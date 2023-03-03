/// Public hooks to look up registered tracks
module BaboonAPI.Hooks.Tracks.TrackLookup

open BaboonAPI.Internal

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
