module internal BaboonAPI.Internal.TrackAccessor

open System.Collections.Generic
open BaboonAPI.Hooks.Tracks
open BaboonAPI.Utility.Coroutines

exception DuplicateTrackrefException of string

let makeTrackData (track: TromboneTrack) (trackindex: int): SingleTrackData =
    let sortOrder =
        match track with
        | :? Sortable as sortable -> sortable.sortOrder
        | _ -> 999 + trackindex

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
                    sort_order = sortOrder,
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
    let mutable tracksByIndex = Array.empty

    let makeTrackLoader () =
        TrackRegistrationEvent.EVENT.invoker.OnRegisterTracks()
            |> Seq.indexed
            |> Seq.map (fun (i, track) -> track.trackref, { track = track; trackIndex = i })
            |> checkForDuplicates

    let onTracksLoaded (loaded: seq<string * RegisteredTrack>) =
        // Our track sequence is already sorted - trackIndex is set above ^
        let sortedTracks = loaded |> Seq.toArray
        tracks <- Map.ofArray sortedTracks
        tracksByIndex <- sortedTracks |> Array.map snd

        let allTracks = tracksByIndex |> Seq.map (fun rt -> rt.track) |> Seq.toList
        TracksLoadedEvent.EVENT.invoker.OnTracksLoaded allTracks

    member _.LoadTracks() =
        makeTrackLoader() |> onTracksLoaded

    member _.LoadTracksAsync () =
        coroutine {
            let task = Async.StartAsTask (async {
                return makeTrackLoader()
            })

            yield WaitForTask(task)

            if task.IsCompletedSuccessfully then
                onTracksLoaded task.Result
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

let allTracks () = trackLoader.TracksByIndex |> Seq.ofArray

let toTrackData (track: TromboneTrack) = makeTrackData track (fetchTrackIndex track.trackref)

let load () =
    trackLoader.LoadTracks()

let loadAsync () =
    trackLoader.LoadTracksAsync()
