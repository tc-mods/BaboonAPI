namespace BaboonAPI.Patch

open System.IO
open System.Runtime.Serialization.Formatters.Binary
open BaboonAPI.Hooks
open HarmonyLib
open UnityEngine

type BaseGameTrackRegistry(songs: SongData) =
    interface Tracks.Callback with
        override this.OnRegisterTracks startIndex = seq {
            for ref, array in Seq.zip songs.data_trackrefs songs.data_tracktitles do
                let track = SingleTrackData()
                track.trackname_long <- array[0]
                track.trackname_short <- array[1]
                track.year <- array[2]
                track.artist <- array[3]
                track.genre <- array[4]
                track.desc <- array[5]
                track.difficulty <- int array[6]
                track.length <- int array[7]
                track.tempo <- int array[8]
                track.trackindex <- startIndex + int array[9]
                track.trackref <- ref

                yield track
        }

[<HarmonyPatch(typeof<SaverLoader>, "loadLevelData")>]
type LoaderPatch() =
    static member Prefix () =
        let path = Application.streamingAssetsPath + "/leveldata/songdata.tchamp"
        if File.Exists path then
            use stream = File.Open (path, FileMode.Open)
            let data = BinaryFormatter().Deserialize(stream) :?> SongData

            Tracks.EVENT.Register (BaseGameTrackRegistry data)
        else
            Debug.Log("Could not find base game songdata.champ")

        false
