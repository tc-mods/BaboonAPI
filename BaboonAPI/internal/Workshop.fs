namespace BaboonAPI.Internal.Workshop

open BaboonAPI.Hooks.Tracks
open BaboonAPI.Hooks.Tracks.Collections
open BaboonAPI.Internal
open BaboonAPI.Internal.BaseGame
open BaboonAPI.Utility
open BepInEx.Logging
open Steamworks

type internal WorkshopCollection(meta: CollectionStrings, sprites: BaseGameCollectionSprites, trackRefs: string seq) =
    inherit BaseTromboneCollection("steamworkshop", meta.name, meta.description)

    override _.LoadSprite() = Coroutines.sync(fun () -> Ok sprites.workshop)
    override _.BuildTrackList() = trackRefs |> Seq.map TrackAccessor.fetchTrack

type internal WorkshopTrackLoader(meta: CollectionStrings, sprites: BaseGameCollectionSprites) =
    static let logger = Logger.CreateLogSource "BaboonAPI.WorkshopTrackLoader"

    let workshopTrackRefs = ResizeArray()
    let fetchSubscribedItems () =
        let count = SteamUGC.GetNumSubscribedItems()
        if count > 0u then
            let items = Array.zeroCreate (int count)
            let n = SteamUGC.GetSubscribedItems(items, count)

            items |> Array.truncate (int n)
        else
            Array.empty

    interface TrackRegistrationEvent.Listener with
        member _.OnRegisterTracks() = seq {
            workshopTrackRefs.Clear()

            let loader = CustomTrackLoaderEvent.EVENT.invoker
            let items = fetchSubscribedItems()

            for pubId in items do
                let mutable size = 0UL
                let mutable folder = ""
                let mutable timestamp = 0u

                if SteamUGC.GetItemInstallInfo (pubId, &size, &folder, 1024u, &timestamp) then
                    match loader.LoadTrack folder with
                    | Some track ->
                        workshopTrackRefs.Add track.trackref
                        yield track
                    | None ->
                        logger.LogWarning $"Failed to load workshop track in '{folder}' ({pubId.m_PublishedFileId})"
        }

    interface TrackCollectionRegistrationEvent.Listener with
        member _.OnRegisterCollections() = seq {
            if SteamUGC.GetNumSubscribedItems() > 0u then
                yield WorkshopCollection (meta, sprites, workshopTrackRefs)
        }
