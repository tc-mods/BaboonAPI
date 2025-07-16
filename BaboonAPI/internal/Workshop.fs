namespace BaboonAPI.Internal.Workshop

open BaboonAPI.Hooks.Tracks
open BaboonAPI.Hooks.Tracks.Collections
open BaboonAPI.Internal
open BaboonAPI.Internal.BaseGame
open BaboonAPI.Utility
open Steamworks

type internal WorkshopCollection(meta: CollectionStrings, sprites: BaseGameCollectionSprites, trackRefs: string seq) =
    inherit BaseTromboneCollection("steamworkshop", meta.name, meta.description)

    override _.LoadSprite() = Coroutines.sync(fun () -> Ok sprites.workshop)
    override _.BuildTrackList() = trackRefs |> Seq.map TrackAccessor.fetchTrack

type internal WorkshopTrackLoader(meta: CollectionStrings, sprites: BaseGameCollectionSprites) =
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

            let loader = CustomTrackLoaderEvent.EVENT.invoker.GetLoader()
            let items = fetchSubscribedItems()

            for pubId in items do
                let mutable size = 0UL
                let mutable folder = ""
                let mutable timestamp = 0u

                if SteamUGC.GetItemInstallInfo (pubId, &size, &folder, 1024u, &timestamp) then
                    let track = loader.LoadTrack folder
                    workshopTrackRefs.Add track.trackref
                    yield track
        }

    interface TrackCollectionRegistrationEvent.Listener with
        member _.OnRegisterCollections() = seq {
            if SteamUGC.GetNumSubscribedItems() > 0u then
                yield WorkshopCollection (meta, sprites, workshopTrackRefs)
        }
