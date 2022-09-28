namespace BaboonAPI.Patch

open System.Reflection.Emit
open BaboonAPI.Hooks.Tracks
open BaboonAPI.Internal
open BepInEx.Logging
open HarmonyLib
open UnityEngine

module SeqUtil =
    let partition predicate (s: seq<'a>) =
        let i = s |> Seq.findIndex predicate
        Seq.take i s, Seq.item i s, Seq.skip (i + 1) s

type private FreePlayLoader() =
    let bundle = AssetBundle.LoadFromFile $"{Application.dataPath}/StreamingAssets/trackassets/freeplay"

    interface LoadedTromboneTrack with
        member this.trackref = "freeplay"
        member this.LoadAudio() =
            null // skip consuming this below.

        member this.LoadBackground() =
            bundle.LoadAsset<GameObject> "BGCam_freeplay"

        member this.Dispose() =
            bundle.Unload true

type GameControllerExtension() =
    static let mutable loadedTrack: LoadedTromboneTrack option = None
    static let logger = Logger.CreateLogSource "BaboonAPI.GameControllerExtension"

    static member fetchTrackTitle i = (TrackAccessor.fetchTrackByIndex i).trackname_long

    static member Infix(instance: GameController) =
        let l: LoadedTromboneTrack =
            if instance.freeplay then
                new FreePlayLoader()
            else
                let track = TrackAccessor.fetchTrackByIndex instance.levelnum
                track.LoadTrack()

        if not instance.freeplay then
            let audio = l.LoadAudio()
            instance.musictrack.clip <- audio.clip
            instance.musictrack.volume <- audio.volume

        let bgObj = Object.Instantiate<GameObject>(
            l.LoadBackground(), Vector3.zero, Quaternion.identity, instance.bgholder.transform)

        bgObj.transform.localPosition <- Vector3.zero
        instance.bgcontroller.fullbgobject <- bgObj
        instance.bgcontroller.songname <- l.trackref

        // Start background task next frame
        instance.StartCoroutine(instance.loadAssetBundleResources ()) |> ignore

        // Usually this should be cleaned up by Unload, but let's just make sure...
        match loadedTrack with
        | Some prev ->
            logger.LogWarning $"Loaded track {prev.trackref} wasn't cleaned up properly"
            prev.Dispose()
        | None -> ()

        loadedTrack <- Some l

        ()

    static member Unload() =
        match loadedTrack with
        | Some l ->
            l.Dispose()
            loadedTrack <- None
        | None -> ()

[<HarmonyPatch>]
type GameControllerPatch() =
    static let tracktitles_f = AccessTools.Field(typeof<GlobalVariables>, nameof GlobalVariables.data_tracktitles)

    static member ReplaceTitleLookups (instructions: CodeInstruction seq): CodeInstruction seq = seq {
        use e = instructions.GetEnumerator ()

        while e.MoveNext () do
            let ins = e.Current
            if ins.LoadsField(tracktitles_f) then
                e.MoveNext() |> ignore
                yield e.Current // ldarg.0
                e.MoveNext() |> ignore
                yield e.Current // ldfld GameController::levelnum
                yield CodeInstruction.Call(typeof<GameControllerExtension>, "fetchTrackTitle", [| typeof<int> |])

                e.MoveNext() |> ignore // ldelem.ref
                e.MoveNext() |> ignore // ldc.i4.0
                e.MoveNext() |> ignore // ldelem.ref
            else
                yield ins
    }

    [<HarmonyTranspiler>]
    [<HarmonyPatch(typeof<GameController>, "Start")>]
    static member TranspileStart(instructions: CodeInstruction seq) : CodeInstruction seq =
        seq {
            let head, _, tail =
                instructions |> SeqUtil.partition (fun ins -> ins.Is(OpCodes.Ldstr, "/StreamingAssets/trackassets/"))

            yield! head |> GameControllerPatch.ReplaceTitleLookups

            // call GameControllerExtension.Infix(this)
            yield CodeInstruction OpCodes.Ldarg_0
            yield CodeInstruction.Call(typeof<GameControllerExtension>, "Infix")

            // Find the index of gameObject = null; (ldnull -> stloc_2)
            let index =
                tail
                |> Seq.pairwise
                |> Seq.findIndex (fun (prev, cur) ->
                    prev.opcode = OpCodes.Ldnull && cur.opcode = OpCodes.Stloc_2)

            // Skip to after stloc_2
            yield! tail |> Seq.skip (index + 2)
        }

    [<HarmonyPrefix>]
    [<HarmonyPatch(typeof<GameController>, "unloadBundles")>]
    static member UnloadPrefix(__instance: GameController) =
        GameControllerExtension.Unload()

        __instance.mySoundAssetBundle.Unload true
        __instance.mySoundAssetBundle <- null

        false
