namespace BaboonAPI.Hooks.Tracks.Collections

open BaboonAPI.Hooks.Tracks
open BaboonAPI.Internal
open BaboonAPI.Utility.Coroutines
open UnityEngine

/// <summary>A lazily-resolved track collection</summary>
[<AbstractClass>]
type public BaseTromboneCollection(id: string, name: string, description: string) =
    abstract BuildTrackList: unit -> TromboneTrack seq

    abstract LoadSprite: unit -> YieldTask<Result<Sprite, string>>

    /// Displayed on the collection settings screen
    abstract folder: string
    default _.folder = ""

    interface TromboneCollection with
        member this.unique_id = id
        member this.name = name
        member this.description = description
        member this.tracks = this.BuildTrackList()

        member this.Resolve index =
            this.LoadSprite() |> map (fun sprite ->
                let art =
                    match sprite with
                    | Ok sprite -> sprite
                    | Error _ -> null

                let tracks =
                    (this :> TromboneCollection).tracks
                    |> Seq.map TrackAccessor.toTrackData
                    |> ResizeArray

                let runtime = tracks |> Seq.sumBy (_.length)

                TrackCollection(
                    _index = index,
                    _unique_id = id,
                    _name = name,
                    _description = description,
                    _art = art,
                    _runtime = runtime,
                    _trackcount = tracks.Count,
                    _folder = this.folder,
                    all_tracks = tracks
                )
            )
