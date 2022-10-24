module BaboonAPI.Internal.TrackAccessor

open System.Collections.Generic
open BaboonAPI.Hooks.Tracks

exception DuplicateTrackrefException of string

let private checkForDuplicates (tracks: seq<string * TromboneTrack>): seq<string * TromboneTrack> = seq {
    let seen = HashSet()
    for trackref, track in tracks do
        if seen.Add trackref then
            yield (trackref, track)
        else
            raise (DuplicateTrackrefException trackref)
}

let private tracks =
    lazy
        EVENT.invoker.OnRegisterTracks(TrackIndexGenerator())
        |> Seq.map (fun track -> track.trackref, track)
        |> checkForDuplicates
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
