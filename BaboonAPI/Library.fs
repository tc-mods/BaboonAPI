namespace BaboonAPI

open BaboonAPI.Hooks.Initializer
open BaboonAPI.Hooks.Saves
open BaboonAPI.Internal
open BaboonAPI.Patch
open BaboonAPI.Utility
open BepInEx
open HarmonyLib

[<BepInPlugin("ch.offbeatwit.baboonapi.plugin", "BaboonAPI", "2.7.3.0")>]
type BaboonPlugin() =
    inherit BaseUnityPlugin()

    let harmony = Harmony("ch.offbeatwit.baboonapi.plugin")

    member this.Awake() =
        GameInitializationEvent.EVENT.Register this
        AsyncGameInitializationEvent.EVENT.Register this
        CustomSaveRegistry.Register this.Info (fun cap ->
            cap.Attach "scores" ScoreStorage.customStorage)

        // Apply the initializer patchset
        harmony.PatchAll(typeof<SafeguardPatch>)
        harmony.PatchAll(typeof<BrandingPatch>)

    member this.TryLoadTracksAsync = Unity.task this {
        try
            yield TrackAccessor.loadAsync() |> this.StartCoroutine
            ScoreStorage.baseGameStorage |> Option.iter (_.firstTimeBackup())
            return Ok ()
        with
        | TrackAccessor.DuplicateTrackrefException trackref ->
            let msg = String.concat "\n" [
                $"Duplicate tracks found with track ID '{trackref}'"
                "Please check your songs folder for duplicates!"
            ]
            return Error { PluginInfo = this.Info
                           Message = msg }
    }

    interface GameInitializationEvent.Listener with
        member this.Initialize() =
            let logger = this.Logger
            logger.LogInfo "Unlocking the secrets of the baboon..."

            GameInitializationEvent.attempt this.Info (fun () ->
                Debug.printDebug logger

                // Apply the rest of the patches
                [
                    typeof<TrackLoaderPatch>
                    typeof<LoaderPatch>
                    typeof<TrackLoadingPatch>
                    typeof<ReloadButtonPatch>
                    typeof<LevelSelectPatch>
                    typeof<GameControllerPatch>
                    typeof<PausePatches>
                    typeof<PreviewPatch>
                    typeof<SaverLoaderPatch>
                    typeof<TrackScorePatches>
                ] |> List.iter harmony.PatchAll

                // We've patched it now so we can call it.
                GlobalVariables.track_collection_loader.buildTrackCollections())

    interface AsyncGameInitializationEvent.Listener with
        member this.Initialize () =
            this.TryLoadTracksAsync()
