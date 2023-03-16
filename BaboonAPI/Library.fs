namespace BaboonAPI

open BaboonAPI.Hooks.Initializer
open BaboonAPI.Hooks.Saves
open BaboonAPI.Internal
open BaboonAPI.Patch
open BepInEx
open HarmonyLib

[<BepInPlugin("ch.offbeatwit.baboonapi.plugin", "BaboonAPI", "2.2.0.0")>]
type BaboonPlugin() =
    inherit BaseUnityPlugin()

    let harmony = Harmony("ch.offbeatwit.baboonapi.plugin")

    member this.Awake() =
        GameInitializationEvent.EVENT.Register this
        CustomSaveRegistry.Register this.Info (fun cap ->
            cap.Attach "scores" ScoreStorage.customStorage)

        // Apply the initializer patchset
        harmony.PatchAll(typeof<BrandingPatch>)

    member this.TryLoadTracks() =
        try
            TrackAccessor.load()
            Ok(())
        with
        | TrackAccessor.DuplicateTrackrefException trackref ->
            let msg = String.concat "\n" [
                $"Duplicate tracks found with track ID '{trackref}'"
                "Please check your songs folder for duplicates!"
            ]
            Error { PluginInfo = this.Info
                    Message = msg }

    interface GameInitializationEvent.Listener with
        member this.Initialize() =
            this.Logger.LogInfo "Unlocking the secrets of the baboon..."

            // Apply the rest of the patches
            GameInitializationEvent.attempt this.Info (fun () ->
                [
                    typeof<TrackCountPatches>
                    typeof<TrackLoaderPatch>
                    typeof<TrackTitlePatches>
                    typeof<LoaderPatch>
                    typeof<GameControllerPatch>
                    typeof<SaverLoaderPatch>
                    typeof<TrackScorePatches>
                ] |> List.iter harmony.PatchAll

                // We've patched it now so we can call it.
                SaverLoader.loadLevelData()

                // First-time backup.
                ScoreStorage.baseGameStorage
                |> Option.iter (fun bgs -> bgs.firstTimeBackup()))
            |> Result.bind this.TryLoadTracks
