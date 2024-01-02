namespace BaboonAPI.Patch

open System.IO
open System.Runtime.Serialization.Formatters.Binary
open BaboonAPI.Hooks.Tracks
open BaboonAPI.Internal
open BaboonAPI.Internal.BaseGame
open BepInEx.Logging
open HarmonyLib
open UnityEngine

[<HarmonyPatch(typeof<SaverLoader>, "loadLevelData")>]
type LoaderPatch() =
    static let logger = Logger.CreateLogSource "BaboonAPI.BaseTracksLoader"

    static member Prefix () =
        let path = $"{Application.streamingAssetsPath}/leveldata/songdata.tchamp"
        if File.Exists path then
            use stream = File.Open (path, FileMode.Open)
            let data = BinaryFormatter().Deserialize(stream) :?> SongData
            let registry = BaseGameTrackRegistry data

            TrackRegistrationEvent.EVENT.Register registry
            ScoreStorage.initialize registry.trackrefs
        else
            logger.LogWarning "Could not find base game songdata.tchamp"

        false
