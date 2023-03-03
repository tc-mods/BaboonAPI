module internal BaboonAPI.Internal.TrackAccessor

open System.Collections.Generic
open BaboonAPI.Hooks.Tracks

exception DuplicateTrackrefException of string

let makeTrackData (track: TromboneTrack) (trackindex: int): SingleTrackData =
    SingleTrackData(trackname_long = track.trackname_long,
                    trackname_short = track.trackname_short,
                    year = track.year,
                    artist = track.artist,
                    desc = track.desc,
                    genre = track.genre,
                    difficulty = track.difficulty,
                    tempo = track.tempo,
                    length = track.length,
                    trackref = track.trackref,
                    trackindex = trackindex)

type RegisteredTrack =
    { track: TromboneTrack
      trackIndex: int }

    member this.asTrackData =
        makeTrackData this.track this.trackIndex

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
        TrackRegistrationEvent.EVENT.invoker.OnRegisterTracks()
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

let fetchRegisteredTrack (ref: string) = tracks.Value[ref]

let tryFetchRegisteredTrack (ref: string) = Map.tryFind ref tracks.Value

let fetchTrackIndex (ref: string) = tracks.Value[ref].trackIndex

let trackCount () = tracksByIndex.Value.Length

let allTracks () = tracksByIndex.Value |> Seq.ofList

let toTrackData (track: TromboneTrack) = makeTrackData track (fetchTrackIndex track.trackref)

let load () =
    tracks.Value |> ignore
