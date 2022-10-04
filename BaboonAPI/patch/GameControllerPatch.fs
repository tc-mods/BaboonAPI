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

type private GameControllerExtension() =
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
        instance.StartCoroutine("loadAssetBundleResources") |> ignore

        // Usually this should be cleaned up by Unload, but let's just make sure...
        match loadedTrack with
        | Some prev ->
            logger.LogWarning $"Loaded track {prev.trackref} wasn't cleaned up properly"
            prev.Dispose()
        | None -> ()

        loadedTrack <- Some l

        ()
        
    static member LoadChart(trackref: string): SavedLevel =
        (TrackAccessor.fetchTrack trackref).LoadChart()

    static member Unload() =
        match loadedTrack with
        | Some l ->
            l.Dispose()
            loadedTrack <- None
        | None -> ()

[<HarmonyPatch>]
type GameControllerPatch() =
    static let tracktitles_f = AccessTools.Field(typeof<GlobalVariables>, nameof GlobalVariables.data_tracktitles)

    [<HarmonyTranspiler>]
    [<HarmonyPatch(typeof<GameController>, "Start")>]
    static member TranspileStart(instructions: CodeInstruction seq) : CodeInstruction seq =
        let matcher = CodeMatcher(instructions)

        // Fix title lookups
        matcher.MatchForward(false, [|
            CodeMatch(fun ins -> ins.LoadsField(tracktitles_f))
            CodeMatch OpCodes.Ldarg_0
            CodeMatch OpCodes.Ldfld
            CodeMatch OpCodes.Ldelem_Ref
            CodeMatch OpCodes.Ldc_I4_0
            CodeMatch OpCodes.Ldelem_Ref
        |]).Repeat(fun matcher ->
            matcher.RemoveInstruction() // remove ldsfld
                .Advance(2) // pos = first ldelem_ref
                .RemoveInstructions(3)
                .InsertAndAdvance(CodeInstruction.Call(typeof<GameControllerExtension>, "fetchTrackTitle", [| typeof<int> |]))
                |> ignore
        ) |> ignore

        let startIndex =
            matcher.Start().MatchForward(false, [|
                CodeMatch(OpCodes.Ldstr, "/StreamingAssets/trackassets/")
            |]).Pos

        let startLabels = matcher.Labels

        let endIndex =
            matcher.MatchForward(true, [|
                CodeMatch OpCodes.Ldnull
                CodeMatch OpCodes.Stloc_2
            |]).Pos

        matcher.RemoveInstructionsInRange(startIndex, endIndex)
            .Start()
            .Advance(startIndex)
            .Insert([|
                CodeInstruction OpCodes.Ldarg_0
                CodeInstruction.Call(typeof<GameControllerExtension>, "Infix")
            |])
            .AddLabels(startLabels) // re-apply start labels
            .InstructionEnumeration()

    [<HarmonyTranspiler>]
    [<HarmonyPatch(typeof<GameController>, "tryToLoadLevel")>]
    static member LoadChartTranspiler(instructions: CodeInstruction seq): CodeInstruction seq =
        let matcher = CodeMatcher(instructions)
        matcher.MatchForward(true, [|
            CodeMatch OpCodes.Ldnull
            CodeMatch OpCodes.Stloc_1
        |]) |> ignore
        
        let endpos = matcher.Pos
        matcher.RemoveInstructionsInRange(0, endpos)
            .Start()
            .Insert([|
                CodeInstruction OpCodes.Ldarg_1
                CodeInstruction.Call(typeof<GameControllerExtension>, "LoadChart", [| typeof<string> |])
                CodeInstruction OpCodes.Stloc_2
            |])
            .InstructionEnumeration()

    [<HarmonyPrefix>]
    [<HarmonyPatch(typeof<GameController>, "unloadBundles")>]
    static member UnloadPrefix(__instance: GameController, ___mySoundAssetBundle: AssetBundle byref) =
        GameControllerExtension.Unload()

        ___mySoundAssetBundle.Unload true
        ___mySoundAssetBundle <- null

        false
