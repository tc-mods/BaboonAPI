namespace BaboonAPI.Internal.Customs

open System.IO
open BaboonAPI.Hooks.Tracks
open BaboonAPI.Hooks.Tracks.Collections
open BaboonAPI.Internal
open BaboonAPI.Internal.BaseGame
open BaboonAPI.Utility
open Newtonsoft.Json
open UnityEngine

type internal CustomCollection(folderPath: string, trackRefs: string seq, meta: TrackCollections.ExternalCollectionMetadata, sprites: BaseGameCollectionSprites) =
    inherit BaseTromboneCollection(Hash128.Compute(folderPath).ToString(), meta.name, meta.description)

    override _.folder = Path.GetFileName(folderPath.TrimEnd('/', '\\'))

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

/// Responsible for loading custom collections
type internal CustomCollectionsRegistry(basePath: string, localizer: StringLocalizer, sprites: BaseGameCollectionSprites) =
    let serializer = JsonSerializer()
    let defaultMeta = TrackCollections.ExternalCollectionMetadata(name = localizer.getLocalizedText("collections_name_custom"), description = "")
    let mutable loadedTrackRefs = Map.empty

    let loadCollectionTracks (loader: CustomTrackLoader) (folderPath: string) =
        let tracks =
            Directory.EnumerateFiles(folderPath, "song.tmb", SearchOption.TopDirectoryOnly)
            |> Seq.map Path.GetDirectoryName
            |> Seq.choose loader.LoadTrack

        (folderPath, tracks)

    let tryLoadMetadata (folderPath: string) =
        let metaPath = Path.Combine(folderPath, "collection_metadata.json")
        if File.Exists metaPath then
            try
                use stream = File.OpenText metaPath
                use reader = new JsonTextReader(stream)
                Some (serializer.Deserialize<TrackCollections.ExternalCollectionMetadata> reader)
            with
            | _ -> None
        else
            None

    let hookTrackRefs (folderPath: string, tracks: TromboneTrack seq) =
        let trackrefs = tracks |> Seq.map (_.trackref) |> Seq.toList
        loadedTrackRefs <- Map.add folderPath trackrefs loadedTrackRefs

        tracks

    interface TrackRegistrationEvent.Listener with
        member this.OnRegisterTracks() =
            loadedTrackRefs <- Map.empty

            let loader = CustomTrackLoaderEvent.EVENT.invoker

            if Directory.Exists basePath then
                Directory.EnumerateDirectories(basePath, "*", SearchOption.TopDirectoryOnly)
                |> Seq.map (loadCollectionTracks loader)
                |> Seq.collect hookTrackRefs
            else
                Seq.empty

    interface TrackCollectionRegistrationEvent.Listener with
        member this.OnRegisterCollections() =
            seq {
                if Directory.Exists basePath then
                    let collections = Directory.EnumerateDirectories(basePath, "*", SearchOption.TopDirectoryOnly)

                    for folder in collections do
                        let meta = tryLoadMetadata folder |> Option.defaultValue defaultMeta

                        match loadedTrackRefs |> Map.tryFind folder with
                        | Some trackRefs when not (List.isEmpty trackRefs) ->
                            yield CustomCollection(folder, trackRefs, meta, sprites)
                        | _ -> ()
            }
