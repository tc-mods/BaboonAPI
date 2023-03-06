/// Public hooks to look up registered tracks
module BaboonAPI.Hooks.Tracks.TrackLookup

open BaboonAPI.Internal
open BaboonAPI.Internal.ScoreStorage

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

/// Highest rank & most recent 5 high scores for a track
type public SavedTrackScore =
    { highestRank: string option
      highScores: int list }

/// <summary>Get the current high scores for a track.</summary>
/// <returns>None if there is no saved data for the trackref, Some otherwise.</returns>
let public trackScore (trackref: string): SavedTrackScore option =
    let storage = getStorageFor trackref
    if storage.IsSaved trackref then
        let score = storage.Load trackref

        Some { highestRank = score.highestRank |> Option.map (fun r -> r.ToString())
               highScores = score.highScores }
    else
        None
