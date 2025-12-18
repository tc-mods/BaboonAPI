namespace BaboonAPI.Internal.DLC

open System.IO
open System.Runtime.Serialization.Formatters.Binary
open BaboonAPI.Hooks.Tracks
open BaboonAPI.Hooks.Tracks.Collections
open BaboonAPI.Internal
open BaboonAPI.Internal.BaseGame
open BaboonAPI.Utility
open Newtonsoft.Json
open Steamworks
open UnityEngine
open UnityEngine.Localization.Settings

type internal DLCCollection(folderPath: string, trackRefs: string seq, meta: TrackCollections.DLCMetadata, sprites: BaseGameCollectionSprites) =
    inherit BaseTromboneCollection(meta.unique_id, meta.dlc_name[int LocalizationSettings.SelectedLocale.SortOrder], meta.dlc_desc[int LocalizationSettings.SelectedLocale.SortOrder])

    override this.folder = "trackassets_dlc/" + Path.GetFileName(folderPath.TrimEnd('/', '\\'))

    override this.LoadSprite() =
        let path =
            [ "cover.png"; "cover.jpg" ]
            |> List.map (fun name -> Path.Combine(folderPath, name))
            |> List.tryFind File.Exists

        match path with
        | Some spritePath ->
            Unity.loadTexture spritePath
            |> (Coroutines.map << Result.map) (fun tex -> Sprite.Create (tex, Rect (0f, 0f, float32 tex.width, float32 tex.height), Vector2.zero))
        | None ->
            Coroutines.sync (fun () -> Ok sprites.custom)

    override this.BuildTrackList() =
        trackRefs |> Seq.map TrackAccessor.fetchTrack

type internal DLCTrackRegistry(basePath: string, sprites: BaseGameCollectionSprites) =
    let serializer = JsonSerializer()
    let mutable loadedTrackRefs = Map.empty

    let tryLoadMetadata (folderPath: string) =
        let metaPath = Path.Combine(folderPath, "metadata.json")
        if File.Exists metaPath then
            try
                use stream = File.OpenText metaPath
                use reader = new JsonTextReader(stream)
                Some (serializer.Deserialize<TrackCollections.DLCMetadata> reader)
            with
            | _ -> None
        else
            None

    /// Returns true if the user owns the DLC in this folder
    let checkOwnership (folderPath: string) =
        match tryLoadMetadata folderPath with
        | Some meta ->
            let appid = AppId_t(uint32 meta.steam_id)
            SteamApps.BIsDlcInstalled appid
        | _ -> false

    member _.hookTrackRefs (folderPath: string, tracks: TromboneTrack seq) =
        let trackrefs = tracks |> Seq.map (_.trackref) |> Seq.toList
        loadedTrackRefs <- Map.add folderPath trackrefs loadedTrackRefs

        tracks

    member _.loadTracks (path: string) =
        let tracks: TromboneTrack seq = seq {
            let dirs = Directory.GetDirectories(path, "*", SearchOption.TopDirectoryOnly)

            let locale = LocalizationSettings.SelectedLocale
            let postfix = locale.Identifier.Code

            for trackdir in dirs do
                let trackref = Path.GetFileName (trackdir.TrimEnd [|'/'|])

                let metadataPath = Path.Combine(trackdir, $"metadata_{postfix}.tmb")
                if File.Exists(metadataPath) then
                    use stream = File.Open (metadataPath, FileMode.Open)
                    let data = BinaryFormatter().Deserialize(stream) :?> SavedLevelMetadata

                    yield BaseGameTrack (trackdir, data, trackref, true)
        }

        (path, tracks)

    interface TrackRegistrationEvent.Listener with
        member this.OnRegisterTracks() =
            if Directory.Exists basePath then
                Directory.EnumerateDirectories(basePath, "*", SearchOption.TopDirectoryOnly)
                |> Seq.filter checkOwnership
                |> Seq.map this.loadTracks
                |> Seq.collect this.hookTrackRefs
            else
                Seq.empty

    interface TrackCollectionRegistrationEvent.Listener with
        member this.OnRegisterCollections() = seq {
            if Directory.Exists basePath then
                let collections = Directory.EnumerateDirectories(basePath, "*", SearchOption.TopDirectoryOnly)

                for folder in collections do
                    let metaOpt = tryLoadMetadata folder
                    let tracksOpt = loadedTrackRefs |> Map.tryFind folder

                    match metaOpt, tracksOpt with
                    | Some meta, Some trackRefs when not (List.isEmpty trackRefs) ->
                        let appid = AppId_t(uint32 meta.steam_id)
                        if SteamApps.BIsDlcInstalled appid then
                            yield DLCCollection(folder, trackRefs, meta, sprites)
                    | _ -> ()
        }
