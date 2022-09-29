﻿namespace BaboonAPI.Patch

open System.IO
open System.Runtime.Serialization.Formatters.Binary
open BaboonAPI.Hooks.Tracks
open HarmonyLib
open UnityEngine

type internal BaseGameLoadedTrack(trackref: string, bundle: AssetBundle) =
    interface LoadedTromboneTrack with
        member this.LoadAudio() =
            Debug.Log $"Loading music_{trackref} from bundle {bundle}"
            let obj = bundle.LoadAsset<GameObject>($"music_{trackref}")
            obj.GetComponent<AudioSource>()

        member this.LoadBackground() =
            bundle.LoadAsset<GameObject> $"BGCam_{trackref}"

        member this.Dispose() =
            bundle.Unload true

        member this.trackref = trackref

type internal BaseGameTrack(trackref: string, data: string[], startIndex: int) =
    interface TromboneTrack with
        member _.trackref = trackref
        member _.trackname_long = data[0]
        member _.trackname_short = data[1]
        member _.year = data[2]
        member _.artist = data[3]
        member _.genre = data[4]
        member _.desc = data[5]
        member _.difficulty = int data[6]
        member _.length = int data[7]
        member _.tempo = int data[8]
        member _.trackindex = startIndex + int data[9]

        member this.LoadTrack() =
            let bundle = AssetBundle.LoadFromFile $"{Application.dataPath}/StreamingAssets/trackassets/{trackref}"
            new BaseGameLoadedTrack (trackref, bundle)

        member this.IsVisible() =
            match trackref with
            | "einefinal" -> GlobalVariables.localsave.progression_trombone_champ
            | _ -> true

type internal BaseGameTrackRegistry(songs: SongData) =
    interface Callback with
        override this.OnRegisterTracks startIndex = seq {
            for ref, array in Seq.zip songs.data_trackrefs songs.data_tracktitles do
                yield BaseGameTrack (ref, array, startIndex)
        }

[<HarmonyPatch(typeof<SaverLoader>, "loadLevelData")>]
type LoaderPatch() =
    static member Prefix () =
        let path = Application.streamingAssetsPath + "/leveldata/songdata.tchamp"
        if File.Exists path then
            use stream = File.Open (path, FileMode.Open)
            let data = BinaryFormatter().Deserialize(stream) :?> SongData

            EVENT.Register (BaseGameTrackRegistry data)
        else
            Debug.Log("Could not find base game songdata.tchamp")

        false
