module internal BaboonAPI.Internal.TrackAccessor

open System.Collections.Generic
open BaboonAPI.Hooks.Tracks
open BaboonAPI.Internal.ScoreStorage
open BaboonAPI.Utility
open BaboonAPI.Utility.Coroutines
open UnityEngine

exception DuplicateTrackrefException of string
exception DuplicateCollectionException of string

let makeSongGraph (track: TromboneTrack) =
    let generate _ =
        Mathf.Clamp(track.difficulty * 10 + Random.Range (-25, 5), 10, 104)

    let graph =
        match track with
        | :? Graphable as graphable ->
            graphable.CreateGraph()
        | _ -> None

    match graph with
    | Some g ->
        g.asArray
    | None ->
        Array.init 5 generate

let makeTrackData (track: TromboneTrack) (trackindex: int): SingleTrackData =
    let sortOrder =
        match track with
        | :? Sortable as sortable -> sortable.sortOrder
        | _ -> 999 + trackindex

    let trackFolder =
        match track with
        | :? FilesystemTrack as fsi -> fsi.folderPath
        | _ -> null

    let graph = makeSongGraph(track)
    let storage = ScoreStorage.getStorageFor track.trackref
    let favorites = GlobalVariables.localsave_favtracks

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
                    graphpoints = graph,
                    is_favorite = favorites.Contains(track.trackref),
                    user_scores = storage.Load(track.trackref).ToBaseGame(),
                    track_folder = trackFolder,
                    json_format = not (storage :? BaseTrackScoreStorage), // don't even worry about it
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

let private checkForDuplicateCollections (collections: TromboneCollection seq): TromboneCollection seq = seq {
    let seen = HashSet()
    for entry in collections do
        if seen.Add entry.unique_id then
            yield entry
        else
            raise (DuplicateCollectionException entry.unique_id)
}

let private hookProgress (onProgress: int -> unit) (coll: 'a seq) = seq {
    for i, entry in Seq.indexed coll do
        yield entry
        onProgress i
}

type TrackLoader() =
    let mutable tracks = Map.empty
    let mutable tracksByIndex = Array.empty
    let mutable collections = List.empty
    let mutable collectionsById = Map.empty

    let collectionSorter (x: TromboneCollection) (y: TromboneCollection) =
        if x.unique_id = "all" then
            -1
        elif y.unique_id = "all" then
            1
        else
            x.name.CompareTo y.name

    let makeTrackLoader (onProgress: Progress -> unit) =
        let tracks =
            TrackRegistrationEvent.EVENT.invoker.OnRegisterTracks()
            |> Seq.indexed
            |> Seq.map (fun (i, track) -> track.trackref, { track = track; trackIndex = i })
            |> checkForDuplicates
            |> hookProgress (fun i -> onProgress (LoadingTracks { loaded = i }))
            |> Seq.toArray

        let collections =
            TrackCollectionRegistrationEvent.EVENT.invoker.OnRegisterCollections()
            |> Seq.sortWith collectionSorter
            |> checkForDuplicateCollections
            |> hookProgress (fun i -> onProgress (LoadingCollections { loaded = i }))
            |> Seq.toList

        (tracks, collections)

    let onTracksLoaded (loadedTracks: array<string * RegisteredTrack>, loadedCollections: TromboneCollection list) =
        // Our track sequence is already sorted - trackIndex is set above ^
        tracks <- Map.ofArray loadedTracks
        tracksByIndex <- loadedTracks |> Array.map snd
        collections <- loadedCollections
        collectionsById <- loadedCollections |> Seq.map (fun coll -> coll.unique_id, coll) |> Map.ofSeq

        { totalTracks = Array.length tracksByIndex; totalCollections = List.length collections }

    member _.LoadTracks onProgress =
        let info = makeTrackLoader onProgress |> onTracksLoaded
        onProgress (FirstStageDone info)

    member _.LoadTracksAsync onProgress =
        Unity.task {
            let task = Async.StartAsTask (async {
                return makeTrackLoader(onProgress)
            })

            yield WaitForTask(task)

            if task.IsCompletedSuccessfully then
                let info = onTracksLoaded task.Result
                onProgress (FirstStageDone info)
                return Ok ()
            elif task.IsFaulted then
                return Error (task.Exception :> exn)
            else
                return Error (exn "Unknown error occured during track loading")
        }

    /// Resolve all track collections asynchronously and update base game about it
    member this.ResolveCollections onProgress =
        Unity.task {
            GlobalVariables.all_track_collections.Clear()

            for index, collection in Seq.indexed collections do
                let! resolved = collection.Resolve(index)
                onProgress (ResolvingCollections { loaded = index + 1 })
                GlobalVariables.all_track_collections.Add resolved

            onProgress (SecondStageDone { loaded = GlobalVariables.all_track_collections.Count })

            TracksLoadedEvent.EVENT.invoker.OnTracksLoaded List.empty
        }

    /// Update all track collections without doing a full async resolve
    member this.UpdateCollections () =
        let resolveTrack (track: TromboneTrack) =
            let rt = this.lookup track.trackref
            makeTrackData rt.track rt.trackIndex

        for gameCollection in GlobalVariables.all_track_collections do
            collectionsById
            |> Map.tryFind gameCollection._unique_id
            |> Option.iter (fun coll ->
                gameCollection.all_tracks.Clear()
                gameCollection.all_tracks.AddRange (Seq.map resolveTrack coll.tracks)
                gameCollection._trackcount <- gameCollection.all_tracks.Count
                gameCollection._runtime <- gameCollection.all_tracks |> Seq.sumBy (_.length)
            )

        TracksLoadedEvent.EVENT.invoker.OnTracksLoaded List.empty

    member _.Tracks = tracks
    member _.TracksByIndex = tracksByIndex

    member _.lookup (trackref: string) = tracks[trackref]

    member _.tryLookup (trackref: string) = Map.tryFind trackref tracks

    member _.tryLookupCollection (unique_id: string) = Map.tryFind unique_id collectionsById

let private trackLoader = TrackLoader()

let fetchTrackByIndex (id: int) : TromboneTrack = trackLoader.TracksByIndex[id].track

let fetchTrack (ref: string) = trackLoader.lookup(ref).track

let fetchRegisteredTrack (ref: string) = trackLoader.lookup ref

let tryFetchRegisteredTrack (ref: string) = trackLoader.tryLookup ref

let fetchTrackIndex (ref: string) = trackLoader.lookup(ref).trackIndex

let trackCount () = trackLoader.Tracks.Count

let allTracks () = trackLoader.TracksByIndex |> Seq.ofArray

let toTrackData (track: TromboneTrack) = makeTrackData track (fetchTrackIndex track.trackref)

let tryFetchCollection (ref: string) = trackLoader.tryLookupCollection ref

let load () =
    trackLoader.LoadTracks ignore

let loadAsync () =
    trackLoader.LoadTracksAsync ignore

let loadAsyncWithProgress onProgress =
    trackLoader.LoadTracksAsync onProgress

let loadCollectionsAsync () =
    trackLoader.ResolveCollections ignore

let loadCollectionsAsyncWithProgress onProgress =
    trackLoader.ResolveCollections onProgress

let updateCollections () =
    trackLoader.UpdateCollections()
