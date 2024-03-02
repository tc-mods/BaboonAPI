namespace BaboonAPI.Internal.BaseGame

open System.IO
open System.Runtime.Serialization.Formatters.Binary
open BaboonAPI.Hooks.Tracks
open BaboonAPI.Internal
open BaboonAPI.Utility
open BaboonAPI.Utility.Unity
open UnityEngine
open UnityEngine.Localization.Settings

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
type public BaseGameTrack internal (data: SavedLevelMetadata, trackref: string) =
    let trackPath = $"{Application.streamingAssetsPath}/trackassets/{trackref}"

    interface TromboneTrack with
        member _.trackname_long = data.trackname_long
        member _.trackname_short = data.trackname_short
        member _.trackref = trackref
        member _.year = data.year
        member _.artist = data.artist
        member _.genre = data.genre
        member _.desc = data.description
        member _.difficulty = data.difficulty
        member _.length = data.length
        member _.tempo = data.tempo

        member this.LoadTrack() =
            let bundle = AssetBundle.LoadFromFile $"{trackPath}/contentbundle"
            new BaseGameLoadedTrack (trackref, bundle)

        member this.IsVisible() =
            match trackref with
            | "einefinal" -> GlobalVariables.localsave.progression_trombone_champ
            | _ -> true

        member this.LoadChart() =
            let path = $"{trackPath}/trackdata.tmb"
            use stream = File.Open(path, FileMode.Open)
            BinaryFormatter().Deserialize(stream) :?> SavedLevel

    interface Sortable with
        member _.sortOrder = data.sort_order

    interface Previewable with
        member this.LoadClip() =
            let path = $"{trackPath}/sample.ogg"

            loadAudioClip (path, AudioType.OGGVORBIS)
            |> (Coroutines.map << Result.map) (fun audioClip -> { Clip = audioClip; Volume = 0.9f })

    interface Graphable with
        member this.CreateGraph() =
            match trackref with
            | "warmup" -> Some (SongGraph.all 10)
            | "einefinal" -> Some (SongGraph.all 104)
            | _ -> None

type internal BaseGameTrackRegistry(path: string, localeSuffixes: string array) =
    interface TrackRegistrationEvent.Listener with
        override this.OnRegisterTracks () = seq {
            let dirs = Directory.GetDirectories(path, "*", SearchOption.TopDirectoryOnly)
            let mutable trackrefs = []

            let locale = LocalizationSettings.SelectedLocale
            let postfix =
                localeSuffixes
                |> Array.tryItem (int locale.SortOrder)
                |> Option.defaultValue "en"

            for trackdir in dirs do
                let trackref = Path.GetFileName (trackdir.TrimEnd [|'/'|])
                trackrefs <- trackref :: trackrefs

                // TODO: fall back to other metadata?
                let metadataPath = Path.Combine(trackdir, $"metadata_{postfix}.tmb")
                if File.Exists(metadataPath) then
                    use stream = File.Open (metadataPath, FileMode.Open)
                    let data = BinaryFormatter().Deserialize(stream) :?> SavedLevelMetadata

                    yield BaseGameTrack (data, trackref)

            ScoreStorage.initialize trackrefs
        }
