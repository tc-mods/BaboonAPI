module BaboonAPI.Internal.TrackAccessor

open System.Collections.Generic
open BaboonAPI.Hooks.Tracks

exception DuplicateTrackrefException of string

type RegisteredTrack =
    { track: TromboneTrack
      trackIndex: int }

let private checkForDuplicates (tracks: seq<string * RegisteredTrack>): seq<string * RegisteredTrack> = seq {
    let seen = HashSet()
    for trackref, track in tracks do
        if seen.Add trackref then
            yield (trackref, track)
        else
            raise (DuplicateTrackrefException trackref)
}

let private tracks =
    lazy
        EVENT.invoker.OnRegisterTracks()
        |> Seq.indexed
        |> Seq.map (fun (i, track) -> track.trackref, { track = track; trackIndex = i })
        |> checkForDuplicates
        |> Map.ofSeq

let private tracksByIndex =
    lazy
        let unsorted =
            tracks.Value.Values |> List.ofSeq

        unsorted
        |> List.permute (fun i -> unsorted[i].trackIndex)

let fetchTrackByIndex (id: int) : TromboneTrack = tracksByIndex.Value[id].track

let fetchTrack (ref: string) = tracks.Value[ref].track

let fetchTrackIndex (ref: string) = tracks.Value[ref].trackIndex

let trackCount () = tracksByIndex.Value.Length

let allTracks () = tracksByIndex.Value |> Seq.ofList
