/// Public hooks to look up registered tracks
module BaboonAPI.Hooks.Tracks.TrackLookup

open BaboonAPI.Internal

/// Look up a track by trackref
let public lookup (trackref: string): TromboneTrack =
    TrackAccessor.fetchTrack trackref

/// Convert a TromboneTrack into a SingleTrackData
let public toTrackData (track: TromboneTrack): SingleTrackData =
    TrackAccessor.toTrackData track
