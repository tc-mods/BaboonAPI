namespace BaboonAPI.Patch

open System.Reflection.Emit
open BaboonAPI.Hooks.Tracks
open BaboonAPI.Internal
open BepInEx.Logging
open HarmonyLib
open UnityEngine

type private FreePlayLoader() =
    let bundle = AssetBundle.LoadFromFile $"{Application.streamingAssetsPath}/trackassets/freeplay"

    interface LoadedTromboneTrack with
        member this.trackref = "freeplay"
        member this.LoadAudio() =
            { Clip = null; Volume = 1.0f } // skip consuming this below.

        member this.LoadBackground _ctx =
            bundle.LoadAsset<GameObject> "BGCam_freeplay"

        member this.SetUpBackgroundDelayed _ _ =
            ()

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
                let track = TrackAccessor.fetchTrack GlobalVariables.chosen_track
                track.LoadTrack()

        if not instance.freeplay then
            let audio = l.LoadAudio()
            instance.musictrack.clip <- audio.Clip
            instance.musictrack.volume <- audio.Volume * GlobalVariables.localsettings.maxvolume_music

        let context = BackgroundContext instance
        let bgObj = Object.Instantiate<GameObject>(
            l.LoadBackground context, Vector3.zero, Quaternion.identity, instance.bgholder.transform)

        bgObj.transform.localPosition <- Vector3.zero
        instance.bgcontroller.fullbgobject <- bgObj
        instance.bgcontroller.songname <- l.trackref

        // Call delayed setup with the now-cloned object
        l.SetUpBackgroundDelayed instance.bgcontroller bgObj

        // Start background task next frame
        instance.StartCoroutine("loadAssetBundleResources") |> ignore

        // Set up pause/resume functionality
        instance.track_is_pausable <-
            match l with
            | :? PauseAware as pauseable -> pauseable.CanResume // `track_is_pausable` actually controls resuming
            | _ -> false

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
        
    static member PauseTrack (controller: PauseCanvasController) =
        match loadedTrack with
        | Some (:? PauseAware as pa) ->
            pa.OnPause (PauseContext controller)
        | _ -> ()

    static member ResumeTrack (controller: PauseCanvasController) =
        match loadedTrack with
        | Some (:? PauseAware as pa) ->
            pa.OnResume (PauseContext controller)
        | _ -> ()

    static member Unload() =
        match loadedTrack with
        | Some l ->
            l.Dispose()
            loadedTrack <- None
        | None -> ()

[<HarmonyPatch>]
type GameControllerPatch() =
    [<HarmonyTranspiler>]
    [<HarmonyPatch(typeof<GameController>, "Start")>]
    static member TranspileStart(instructions: CodeInstruction seq) : CodeInstruction seq =
        let matcher = CodeMatcher(instructions)

        // from `string text = "/trackassets/";`
        let startIndex =
            matcher
                .Start()
                .MatchForward(false, [|
                    CodeMatch(OpCodes.Ldstr, "/trackassets/")
                |])
                .ThrowIfInvalid("Could not find start of injection point in GameController#Start")
                .Pos

        let startLabels = matcher.Labels

        // until `gameObject = null`
        let endIndex =
            matcher
                .MatchForward(true, [|
                    CodeMatch OpCodes.Ldnull
                    CodeMatch OpCodes.Stloc_2
                    CodeMatch OpCodes.Ldc_I4_6
                |])
                .ThrowIfInvalid("Could not find end of injection point in GameController#Start")
                .Pos - 1 // back up to stloc_2

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
        let matcher =
            CodeMatcher(instructions)
                .MatchForward(true, [|
                    CodeMatch OpCodes.Ldnull
                    CodeMatch OpCodes.Stloc_2
                |])
                .ThrowIfInvalid("Could not find injection point in GameController#tryToLoadLevel")

        let endpos = matcher.Pos
        matcher.RemoveInstructionsInRange(0, endpos)
            .Start()
            .Insert([|
                CodeInstruction OpCodes.Ldarg_1
                CodeInstruction.Call(typeof<GameControllerExtension>, "LoadChart", [| typeof<string> |])
                CodeInstruction OpCodes.Stloc_3
            |])
            .InstructionEnumeration()

    [<HarmonyPrefix>]
    [<HarmonyPatch(typeof<GameController>, "unloadBundles")>]
    static member UnloadPrefix(__instance: GameController, ___mySoundAssetBundle: AssetBundle byref) =
        GameControllerExtension.Unload()

        ___mySoundAssetBundle.Unload true
        ___mySoundAssetBundle <- null

        false

[<HarmonyPatch>]
type PausePatches() =
    [<HarmonyPostfix>]
    [<HarmonyPatch(typeof<PauseCanvasController>, "showPausePanel")>]
    static member PausePostfix(__instance: PauseCanvasController) =
        GameControllerExtension.PauseTrack __instance
        ()
    
    [<HarmonyPostfix>]
    [<HarmonyPatch(typeof<PauseCanvasController>, "resumeFromPause")>]
    static member ResumePostfix(__instance: PauseCanvasController) =
        GameControllerExtension.ResumeTrack __instance
        ()
