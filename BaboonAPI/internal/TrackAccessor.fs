module internal BaboonAPI.Internal.TrackAccessor

open System.Collections.Generic
open BaboonAPI.Hooks.Tracks
open BaboonAPI.Utility.Coroutines

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

type TrackLoader() =
    let mutable tracks = Map.empty
    let mutable tracksByIndex = []

    let makeTrackLoader () =
        TrackRegistrationEvent.EVENT.invoker.OnRegisterTracks()
            |> Seq.indexed
            |> Seq.map (fun (i, track) -> track.trackref, { track = track; trackIndex = i })
            |> checkForDuplicates

    member _.LoadTracks() =
        tracks <- makeTrackLoader() |> Map.ofSeq

        let unsorted = tracks.Values |> List.ofSeq
        tracksByIndex <- unsorted |> List.permute (fun i -> unsorted[i].trackIndex)

        let allTracks = tracks.Values |> Seq.map (fun rt -> rt.track) |> Seq.toList
        TracksLoadedEvent.EVENT.invoker.OnTracksLoaded allTracks

    member _.LoadTracksAsync () =
        let onComplete output =
            tracks <- output
            let unsorted = tracks.Values |> List.ofSeq
            tracksByIndex <- unsorted |> List.permute (fun i -> unsorted[i].trackIndex)

            let allTracks = tracks.Values |> Seq.map (fun rt -> rt.track) |> Seq.toList
            TracksLoadedEvent.EVENT.invoker.OnTracksLoaded allTracks

        coroutine {
            let task = Async.StartAsTask (async {
                return makeTrackLoader() |> Map.ofSeq
            })

            yield WaitForTask(task)

            if task.IsCompletedSuccessfully then
                onComplete task.Result
            else if task.IsFaulted then
                raise task.Exception
        }

    member _.Tracks = tracks
    member _.TracksByIndex = tracksByIndex

    member _.lookup (trackref: string) = tracks[trackref]

    member _.tryLookup (trackref: string) = Map.tryFind trackref tracks

let private trackLoader = TrackLoader()

let fetchTrackByIndex (id: int) : TromboneTrack = trackLoader.TracksByIndex[id].track

let fetchTrack (ref: string) = trackLoader.lookup(ref).track

let fetchRegisteredTrack (ref: string) = trackLoader.lookup ref

let tryFetchRegisteredTrack (ref: string) = trackLoader.tryLookup ref

let fetchTrackIndex (ref: string) = trackLoader.lookup(ref).trackIndex

let trackCount () = trackLoader.Tracks.Count

let allTracks () = trackLoader.TracksByIndex |> Seq.ofList

let toTrackData (track: TromboneTrack) = makeTrackData track (fetchTrackIndex track.trackref)

let load () =
    trackLoader.LoadTracks()

let loadAsync () =
    trackLoader.LoadTracksAsync()
