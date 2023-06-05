namespace BaboonAPI.Patch

open System.IO
open System.Runtime.Serialization.Formatters.Binary
open BaboonAPI.Hooks.Tracks
open BaboonAPI.Internal
open BaboonAPI.Utility
open BaboonAPI.Utility.Unity
open BepInEx.Logging
open HarmonyLib
open UnityEngine

type internal BaseGameLoadedTrack(trackref: string, bundle: AssetBundle) =
    interface LoadedTromboneTrack with
        member this.LoadAudio() =
            let obj = bundle.LoadAsset<GameObject>($"music_{trackref}")
            let src = obj.GetComponent<AudioSource>()
            { Clip = src.clip; Volume = src.volume }

        member this.LoadBackground _ctx =
            bundle.LoadAsset<GameObject> $"BGCam_{trackref}"

        member this.Dispose() =
            bundle.Unload true

        member this.SetUpBackgroundDelayed _ _ =
            ()

        member this.trackref = trackref

    interface PauseAware with
        member this.CanResume = true

        member this.OnPause _ = ()

        member this.OnResume _ = ()

type internal BaseGameTrack(data: string[]) =
    interface TromboneTrack with
        member _.trackname_long = data[0]
        member _.trackname_short = data[1]
        member _.trackref = data[2]
        member _.year = data[3]
        member _.artist = data[4]
        member _.genre = data[5]
        member _.desc = data[6]
        member _.difficulty = int data[7]
        member _.length = int data[8]
        member _.tempo = int data[9]

        member this.LoadTrack() =
            let trackref = (this :> TromboneTrack).trackref
            let bundle = AssetBundle.LoadFromFile $"{Application.streamingAssetsPath}/trackassets/{trackref}"
            new BaseGameLoadedTrack (trackref, bundle)

        member this.IsVisible() =
            let trackref = (this :> TromboneTrack).trackref
            match trackref with
            | "einefinal" -> GlobalVariables.localsave.progression_trombone_champ
            | _ -> true

        member this.LoadChart() =
            let trackref = (this :> TromboneTrack).trackref
            let path = $"{Application.streamingAssetsPath}/leveldata/{trackref}.tmb"
            use stream = File.Open(path, FileMode.Open)
            BinaryFormatter().Deserialize(stream) :?> SavedLevel

    interface Previewable with
        member this.LoadClip() =
            let trackref = (this :> TromboneTrack).trackref
            let path = $"{Application.streamingAssetsPath}/trackclips/{trackref}-sample.ogg"

            loadAudioClip (path, AudioType.OGGVORBIS)
            |> Coroutines.map (Result.map (fun audioClip -> { Clip = audioClip; Volume = 0.9f }))

type internal BaseGameTrackRegistry(songs: SongData) =
    /// List of base game trackrefs
    member _.trackrefs =
        songs.data_tracktitles
        |> Seq.map (fun data -> data[2])
        |> Seq.toList

    interface TrackRegistrationEvent.Listener with
        override this.OnRegisterTracks () = seq {
            for array in songs.data_tracktitles do
                yield BaseGameTrack array
        }

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
