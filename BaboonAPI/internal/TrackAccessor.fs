module BaboonAPI.Internal.TrackAccessor

open BaboonAPI.Hooks.Tracks

let private tracks =
    lazy
        EVENT.invoker.OnRegisterTracks(0)
        |> Seq.map (fun track -> track.trackref, track)
        |> Map.ofSeq

let private tracksByIndex =
    lazy
        let unsorted =
            tracks.Value.Values |> List.ofSeq

        unsorted
        |> List.permute (fun i -> unsorted[i].trackindex)

let fetchTrackByIndex (id: int) : TromboneTrack = tracksByIndex.Value[id]

let fetchTrack (ref: string) = tracks.Value[ref]

let trackCount () = tracksByIndex.Value.Length

let allTracks () = tracksByIndex.Value |> Seq.ofList
