/// Public hooks to look up registered tracks
module BaboonAPI.Hooks.Tracks.TrackLookup

open System
open BaboonAPI.Internal
open BaboonAPI.Internal.ScoreStorage
open BaboonAPI.Utility
open UnityEngine

/// Look up a track by trackref
let public lookup (trackref: string): TromboneTrack =
    TrackAccessor.fetchTrack trackref

/// Look up a track by trackref, returning None if the track doesn't exist.
let public tryLookup (trackref: string): TromboneTrack option =
    TrackAccessor.tryFetchRegisteredTrack trackref
    |> Option.map (fun rt -> rt.track)

/// Convert a TromboneTrack into a SingleTrackData
let public toTrackData (track: TromboneTrack): SingleTrackData =
    TrackAccessor.toTrackData track

/// Get a list of every track currently loaded
let public allTracks (): TromboneTrack list =
    TrackAccessor.allTracks()
    |> Seq.map (fun rt -> rt.track)
    |> Seq.toList

/// <summary>Reload the list of tracks synchronously</summary>
/// <remarks>NOTE: This function does not reload collections!</remarks>
[<Obsolete("Use the methods in TrackReloader instead")>]
let public reload () =
    TrackAccessor.load()

/// <summary>Reload the list of tracks asynchronously.</summary>
/// <remarks>Note this method does not reload collections.</remarks>
/// <returns>A Unity coroutine that must be started using StartCoroutine.</returns>
[<Obsolete("Use the methods in TrackReloader instead")>]
let public reloadAsync () =
    TrackAccessor.loadAsync()
    |> Coroutines.each ignore

/// <summary>Reload the list of tracks and collections asynchronously.</summary>
/// <returns>A Unity coroutine that must be started using StartCoroutine.</returns>
[<Obsolete("Use the methods in TrackReloader instead")>]
let public reloadAllAsync (_: MonoBehaviour) =
    Coroutines.coroutine {
        match! TrackAccessor.loadAsync() with
        | Ok () ->
            do! TrackAccessor.loadCollectionsAsync()
        | Error err ->
            Debug.LogError err  // TODO
            ()
    }

/// Highest rank & most recent 5 high scores for a track
type public SavedTrackScore =
    { highestRank: string
      highScores: int list }

/// <summary>Get the current high scores for a track.</summary>
/// <returns>None if there is no saved data for the trackref, Some otherwise.</returns>
let public lookupScore (trackref: string): SavedTrackScore option =
    let storage = getStorageFor trackref
    if storage.IsSaved trackref then
        let score = storage.Load trackref

        Some { highestRank = rankString score.highestRank
               highScores = score.highScores }
    else
        None
