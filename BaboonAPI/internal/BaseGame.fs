namespace BaboonAPI.Internal.BaseGame

open System.IO
open System.Runtime.Serialization.Formatters.Binary
open BaboonAPI.Hooks.Tracks
open BaboonAPI.Utility
open BaboonAPI.Utility.Unity
open UnityEngine

/// Base game loaded track, emulates base game behaviour for charts
type public BaseGameLoadedTrack internal (trackref: string, bundle: AssetBundle) =
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

/// Base game TromboneTrack
type public BaseGameTrack internal (data: string[]) =
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

    interface Graphable with
        member this.CreateGraph() =
            match (this :> TromboneTrack).trackref with
            | "warmup" -> Some (SongGraph.all 10)
            | "einefinal" -> Some (SongGraph.all 104)
            | _ -> None

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
