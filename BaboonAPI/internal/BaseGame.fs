namespace BaboonAPI.Internal.BaseGame

open System.IO
open System.Runtime.Serialization.Formatters.Binary
open BaboonAPI.Hooks.Tracks
open BaboonAPI.Hooks.Tracks.Collections
open BaboonAPI.Internal
open BaboonAPI.Utility.Coroutines
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
            | "einefinal" | "tchampmedley" -> GlobalVariables.localsave.progression_trombone_champ
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
            |> (map << Result.map) (fun audioClip -> { Clip = audioClip; Volume = 0.9f })

    interface Graphable with
        member this.CreateGraph() =
            match trackref with
            | "warmup" -> Some (SongGraph.all 10)
            | "einefinal" -> Some (SongGraph.all 104)
            | _ -> None

/// Base game has an array of sprites for its collections, this maps them to readable names
type internal BaseGameCollectionSprites(sprites: Sprite array) =
    member _.baseGame = sprites[0]
    member _.tootmaker = sprites[1]
    member _.custom = sprites[2]
    member _.favorites = sprites[3]
    member _.allTracks = sprites[4]

type internal BaseGameTrackCollection(localizer: StringLocalizer, sprites: BaseGameCollectionSprites) =
    inherit BaseTromboneCollection("default", localizer.getLocalizedText("collections_name_default"), localizer.getLocalizedText("collections_desc_default"))

    override this.LoadSprite() =
        sync (fun () -> Ok sprites.baseGame)

    override this.BuildTrackList() =
        TrackAccessor.allTracks()
        |> Seq.map _.track
        |> Seq.filter (fun t -> t :? BaseGameTrack)

type internal AllTracksCollection(localizer: StringLocalizer, sprites: BaseGameCollectionSprites) =
    inherit BaseTromboneCollection("all", localizer.getLocalizedText("collections_name_all"), localizer.getLocalizedText("collections_desc_all"))

    override this.LoadSprite() =
        sync (fun () -> Ok sprites.allTracks)

    override this.BuildTrackList() =
        TrackAccessor.allTracks()
        |> Seq.map _.track

type internal FavoriteTracksCollection(localizer: StringLocalizer, sprites: BaseGameCollectionSprites) =
    inherit BaseTromboneCollection("favorites", localizer.getLocalizedText("collections_name_favorites"), localizer.getLocalizedText("collections_desc_favorites"))

    override this.LoadSprite() =
        sync (fun () -> Ok sprites.favorites)

    override this.BuildTrackList() =
        TrackAccessor.allTracks()
        |> Seq.map _.track
        |> Seq.filter (TrackAccessor.toTrackData >> _.is_favorite)

type internal BaseGameTrackRegistry(path: string, localizer: StringLocalizer, sprites: BaseGameCollectionSprites) =
    interface TrackRegistrationEvent.Listener with
        override this.OnRegisterTracks () = seq {
            let dirs = Directory.GetDirectories(path, "*", SearchOption.TopDirectoryOnly)
            let mutable trackrefs = []

            let locale = LocalizationSettings.SelectedLocale
            let postfix = locale.Identifier.Code

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

    interface TrackCollectionRegistrationEvent.Listener with
        member this.OnRegisterCollections() = seq {
            yield BaseGameTrackCollection (localizer, sprites)
            yield FavoriteTracksCollection (localizer, sprites)
            yield AllTracksCollection (localizer, sprites)
        }
