namespace BaboonAPI.Patch

open BaboonAPI.Hooks.Tracks
open BaboonAPI.Internal.BaseGame
open BepInEx.Logging
open HarmonyLib
open UnityEngine

[<HarmonyPatch(typeof<SaverLoader>, "loadAllTrackMetadata")>]
type LoaderPatch() =
    static let logger = Logger.CreateLogSource "BaboonAPI.BaseTracksLoader"

    static member Prefix (___locale_suffixes: string array inref) =
        let path = $"{Application.streamingAssetsPath}/trackassets"
        TrackRegistrationEvent.EVENT.Register (BaseGameTrackRegistry (path, ___locale_suffixes))

        false
