namespace BaboonAPI.Patch

open BaboonAPI.Hooks.Tracks
open BaboonAPI.Internal
open BaboonAPI.Internal.BaseGame
open HarmonyLib
open UnityEngine

[<HarmonyPatch(typeof<SaverLoader>, "loadAllTrackMetadata")>]
type LoaderPatch() =
    static member Prefix () =
        let path = $"{Application.streamingAssetsPath}/trackassets"
        TrackRegistrationEvent.EVENT.Register (BaseGameTrackRegistry path)

        false

[<HarmonyPatch(typeof<LanguageChanger>, "loadMetadata")>]
type LanguageChangerPatch() =
    static member Prefix () =
        TrackAccessor.load()

        false
