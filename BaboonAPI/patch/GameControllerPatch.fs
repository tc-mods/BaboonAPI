namespace BaboonAPI.Patch

open System.Reflection.Emit
open BaboonAPI.Hooks.Tracks
open BaboonAPI.Internal
open BaboonAPI.Utility
open BepInEx.Logging
open HarmonyLib
open UnityEngine

type private FreePlayLoader() =
    let bundle = AssetBundle.LoadFromFile $"{Application.streamingAssetsPath}/trackassets/freeplay/contentbundle"

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
            let onAudioLoaded (audio: TrackAudio) =
                instance.musictrack.clip <- audio.Clip
                instance.musictrack_cliplength <- audio.Clip.length
                instance.musictrack.volume <- audio.Volume * GlobalVariables.localsettings.maxvolume_music

                if GlobalVariables.turbomode then
                    instance.musictrack.pitch <- 2f

            match l with
            | :? AsyncAudioAware as aaa ->
                aaa.LoadAudio()
                |> Coroutines.each (fun r ->
                    match r with
                    | Ok audio -> onAudioLoaded audio
                    | Error err -> logger.LogError $"Failed to load audio: {err}")
                |> instance.StartCoroutine
                |> ignore
            | _ ->
                onAudioLoaded (l.LoadAudio())

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

        // Return smooth_scrolling_move_mult
        if GlobalVariables.turbomode then
            2f
        else
            GlobalVariables.practicemode

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
    static let freeplay_f = AccessTools.Field(typeof<GameController>, "freeplay")
    static let file_exists_m = AccessTools.Method(typeof<System.IO.File>, "Exists")

    [<HarmonyTranspiler>]
    [<HarmonyPatch(typeof<GameController>, "Start")>]
    static member TranspileStart(instructions: CodeInstruction seq) : CodeInstruction seq =
        let matcher = CodeMatcher(instructions)

        // from `string text = "";`
        let startIndex =
            matcher
                .Start()
                .MatchForward(false, [|
                    CodeMatch (OpCodes.Ldstr, "")
                    CodeMatch (fun ins -> ins.IsStloc())
                    CodeMatch OpCodes.Ldarg_0
                    CodeMatch (fun ins -> ins.LoadsField(freeplay_f))
                    CodeMatch OpCodes.Brtrue
                |])
                .ThrowIfInvalid("Could not find start of injection point in GameController#Start")
                .Pos

        let startLabels = matcher.Labels

        // until `gameObject = null`
        let endIndex =
            matcher
                .MatchForward(true, [|
                    CodeMatch OpCodes.Ldnull
                    CodeMatch (fun ins -> ins.IsStloc())
                    CodeMatch OpCodes.Br
                |])
                .ThrowIfInvalid("Could not find end of injection point in GameController#Start")
                .Pos - 1 // back up to stloc

        matcher.RemoveInstructionsInRange(startIndex, endIndex)
            .Start()
            .Advance(startIndex)
            .Insert([|
                CodeInstruction OpCodes.Ldarg_0
                CodeInstruction OpCodes.Ldarg_0
                CodeInstruction.Call(typeof<GameControllerExtension>, "Infix")
                CodeInstruction.StoreField (typeof<GameController>, "smooth_scrolling_move_mult")
            |])
            .AddLabels(startLabels) // re-apply start labels
            .InstructionEnumeration()

    [<HarmonyTranspiler>]
    [<HarmonyPatch(typeof<GameController>, "tryToLoadLevel")>]
    static member LoadChartTranspiler(instructions: CodeInstruction seq): CodeInstruction seq =
        let matcher = CodeMatcher(instructions)

        let existsLabels =
            matcher
                .MatchForward(false, [|
                    CodeMatch OpCodes.Ldloc_0
                    CodeMatch (fun ins -> ins.Calls(file_exists_m))
                    CodeMatch OpCodes.Brfalse
                |])
                .ThrowIfInvalid("Could not find File.Exists call in GameController#tryToLoadLevel")
                .Labels
        matcher
            .RemoveInstructions(3)
            .AddLabels(existsLabels)

            // Find the start of the injection point
            .MatchForward(false, [|
                CodeMatch OpCodes.Ldarg_2
                CodeMatch OpCodes.Brtrue
            |])
            .ThrowIfInvalid("Could not find start of injection point in GameController#tryToLoadLevel")
            |> ignore

        let startPos = matcher.Pos
        let startLabels = matcher.Labels
        let endPos =
           matcher
                .MatchForward(true, [|
                    CodeMatch (fun ins -> ins.IsLdloc())
                    CodeMatch OpCodes.Callvirt
                    CodeMatch OpCodes.Ldnull
                    CodeMatch (fun ins -> ins.IsStloc())
                |])
                .ThrowIfInvalid("Could not find end of injection point in GameController#tryToLoadLevel")
                .Pos

        matcher.RemoveInstructionsInRange(startPos, endPos)
            .Start()
            .Advance(startPos)
            .Insert([|
                CodeInstruction OpCodes.Ldarg_1
                CodeInstruction.Call(typeof<GameControllerExtension>, "LoadChart", [| typeof<string> |])
                CodeInstruction (OpCodes.Stloc_S, 12)
            |])
            .AddLabels(startLabels)
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
