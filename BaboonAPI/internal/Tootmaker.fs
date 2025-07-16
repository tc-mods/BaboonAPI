namespace BaboonAPI.Internal.Tootmaker

open System.Collections
open System.IO
open BaboonAPI.Hooks.Tracks
open BaboonAPI.Hooks.Tracks.Collections
open BaboonAPI.Internal
open BaboonAPI.Internal.BaseGame
open BaboonAPI.Utility
open BaboonAPI.Utility.Coroutines
open HarmonyLib
open Newtonsoft.Json
open UnityEngine

/// A loaded base-game custom track
type public LoadedTootmakerTrack internal (trackref: string, folderPath: string) =
    static let load_bg_m = AccessTools.Method(typeof<GameController>, "loadCustomBG")

    interface LoadedTromboneTrack with
        member this.trackref = trackref

        member this.LoadBackground ctx = ctx.controller.custombg_prefab
        member this.LoadAudio() =
            // We use AsyncAudioAware to load audio, stub this out
            failwith "crimes have occurred"

        member this.SetUpBackgroundDelayed controller bg =
            controller.StartCoroutine(unbox<IEnumerator> (load_bg_m.Invoke (controller.gamecontroller, [|null|]))) |> ignore

        member this.Dispose() =
            ()

    interface AsyncAudioAware with
        member this.LoadAudio() =
            let extensions = [
                "ogg", AudioType.OGGVORBIS
                "mp3", AudioType.MPEG
                "wav", AudioType.WAV
                "aiff", AudioType.AIFF
            ]
            let path =
                extensions
                |> List.map (fun (ext, typ) -> Path.Combine(folderPath, $"song.{ext}"), typ)
                |> List.tryFind (fst >> File.Exists)

            match path with
            | Some (audioPath, audioType) ->
                Unity.loadAudioClip (audioPath, audioType)
                |> (map << Result.map) (fun clip -> { Clip = clip; Volume = 1f })
            | None ->
                sync (fun () -> Error "Could not find valid audio file")

    interface PauseAware with
        member this.CanResume = true
        member this.OnPause ctx = ()
        member this.OnResume ctx = ()

/// A base-game custom track
type public TootmakerTrack internal (data: SongDataCustom, folderPath: string) =
    interface TromboneTrack with
        member _.trackname_short = data.shortName
        member _.trackname_long = data.name
        member _.trackref = data.trackRef
        member _.year = data.year
        member _.artist = data.author
        member _.genre = data.genre
        member _.desc = data.description
        member _.difficulty = data.difficulty
        member _.length = Mathf.FloorToInt(data.endpoint / (data.tempo / 60f))
        member _.tempo = int data.tempo

        member _.IsVisible() = true
        member _.LoadChart() =
            SavedLevel(
                savedleveldata = ResizeArray(data.notes),
                bgdata = ResizeArray(data.bgdata),
                improv_zones = ResizeArray(data.improv_zones),
                endpoint = data.endpoint,
                lyricspos = (data.lyrics |> Seq.map (fun l -> [| l.bar; 0f |]) |> ResizeArray),
                lyricstxt = (data.lyrics |> Seq.map (_.text) |> ResizeArray),
                note_color_start = data.note_color_start,
                note_color_end = data.note_color_end,
                savednotespacing = data.savednotespacing,
                tempo = data.tempo,
                timesig = data.timesig
            )

        member _.LoadTrack() = new LoadedTootmakerTrack(data.trackRef, folderPath)

    interface Previewable with
        member _.LoadClip() =
            let clipPath = Path.Combine(folderPath, "preview.ogg")
            if File.Exists clipPath then
                Unity.loadAudioClip (clipPath, AudioType.OGGVORBIS)
                |> (map << Result.map) (fun clip -> { Clip = clip; Volume = 0.9f })
            else
                sync (fun () -> Error "No preview.ogg available")

    interface FilesystemTrack with
        member _.folderPath = folderPath

type internal TootmakerCollection(folderPath: string, meta: CollectionStrings, sprites: BaseGameCollectionSprites) =
    inherit BaseTromboneCollection("tootmaker", meta.name, meta.description)

    override _.folder = Path.GetFileName(folderPath.TrimEnd('/', '\\'))
    override _.LoadSprite() = sync(fun () -> Ok sprites.tootmaker)
    override this.BuildTrackList() =
        TrackAccessor.allTracks()
        |> Seq.map _.track
        |> Seq.filter (fun t -> t :? TootmakerTrack)

type internal TootmakerTrackRegistry(path: string, localizer: StringLocalizer, sprites: BaseGameCollectionSprites) =
    let serializer = JsonSerializer()
    let meta = { name = localizer.getLocalizedText("collections_name_tootmaker")
                 description = localizer.getLocalizedText("collections_desc_tootmaker") }

    interface TrackRegistrationEvent.Listener with
        member this.OnRegisterTracks() = seq {
            if Directory.Exists path then
                let folders = Directory.EnumerateDirectories(path, "*", SearchOption.TopDirectoryOnly)
                for folderPath in folders do
                    let songPath = Path.Combine(folderPath, "song.tmb")
                    if File.Exists songPath then
                        use stream = File.OpenText songPath
                        use reader = new JsonTextReader(stream)
                        let data = serializer.Deserialize<SongDataCustom> reader
                        yield TootmakerTrack (data, folderPath)
        }

    interface TrackCollectionRegistrationEvent.Listener with
        member this.OnRegisterCollections() =
            if Directory.Exists path then
                Seq.singleton (TootmakerCollection (path, meta, sprites))
            else
                Seq.empty
