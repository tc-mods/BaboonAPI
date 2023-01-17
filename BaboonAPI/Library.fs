namespace BaboonAPI

open BaboonAPI.Hooks.Initializer
open BaboonAPI.Internal
open BaboonAPI.Patch
open BepInEx
open HarmonyLib

[<BepInPlugin("ch.offbeatwit.baboonapi.plugin", "BaboonAPI", "2.0.0.0")>]
type BaboonPlugin() =
    inherit BaseUnityPlugin()

    let harmony = Harmony("ch.offbeatwit.baboonapi.plugin")

    member this.Awake() =
        GameInitializationEvent.EVENT.Register this

        // Apply the initializer patchset
        harmony.PatchAll(typeof<BrandingPatch>)

    interface GameInitializationEvent.Listener with
        member this.Initialize() =
            this.Logger.LogInfo "Hello from BaboonAPI!"

            // Apply the rest of the patches
            GameInitializationEvent.attempt this.Info (fun () ->
                [
                    typeof<TrackCountPatches>
                    typeof<TrackLoaderPatch>
                    typeof<TrackTitlePatches>
                    typeof<LoaderPatch>
                    typeof<GameControllerPatch>
                    typeof<SaverLoaderPatch>
                ] |> List.iter harmony.PatchAll

                // We've patched it now so we can call it.
                SaverLoader.loadLevelData()

                // Load all the tracks so we catch if something goes wrong
                TrackAccessor.load()
                )
