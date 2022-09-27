module BaboonAPI.Internal.TrackAccessor

open BaboonAPI.Hooks

let private tracks =
    lazy
        Tracks.EVENT.invoker.OnRegisterTracks(0)
        |> Seq.map (fun track -> track.trackref, track)
        |> Map.ofSeq

let private tracksByIndex =
    lazy
        let unsorted =
            tracks.Value.Values |> List.ofSeq

        unsorted
        |> List.permute (fun i -> unsorted[i].trackindex)

let fetchTrackByIndex (id: int) : SingleTrackData = tracksByIndex.Value[id]

let fetchTrack (ref: string) = tracks.Value[ref]

let trackCount () = tracksByIndex.Value.Length
