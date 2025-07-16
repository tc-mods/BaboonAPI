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
    let loadedTrackRefs = ResizeArray()

    let loadCollectionTracks (loader: CustomTrackLoader) (folderPath: string) =
        Directory.EnumerateFiles(folderPath, "song.tmb", SearchOption.TopDirectoryOnly)
            |> Seq.map Path.GetDirectoryName
            |> Seq.map loader.LoadTrack

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

    let hookTrackRefs (tracks: TromboneTrack seq) = seq {
        for t in tracks do
            loadedTrackRefs.Add t.trackref
            yield t
    }

    interface TrackRegistrationEvent.Listener with
        member this.OnRegisterTracks() =
            loadedTrackRefs.Clear()

            let loader = CustomTrackLoaderEvent.EVENT.invoker.GetLoader()

            if Directory.Exists basePath then
                Directory.EnumerateDirectories(basePath, "*", SearchOption.TopDirectoryOnly)
                |> Seq.collect (loadCollectionTracks loader)
                |> hookTrackRefs
            else
                Seq.empty

    interface TrackCollectionRegistrationEvent.Listener with
        member this.OnRegisterCollections() =
            seq {
                if Directory.Exists basePath then
                    let collections = Directory.EnumerateDirectories(basePath, "*", SearchOption.TopDirectoryOnly)

                    for folder in collections do
                        let meta = tryLoadMetadata folder |> Option.defaultValue defaultMeta

                        yield CustomCollection(folder, loadedTrackRefs, meta, sprites)
            }
