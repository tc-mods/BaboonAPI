/// <summary>
/// API for registering new tracks.
/// </summary>
/// <remarks>
/// <code>open BaboonAPI.Hooks
///
///member _.Awake () =
///    Tracks.EVENT.Register MyTrackRegistrationListener()
/// </code>
/// </remarks>
module BaboonAPI.Hooks.Tracks

open System
open BaboonAPI.Event
open UnityEngine

/// Loaded track assets, disposed when a level ends
type public LoadedTromboneTrack =
    inherit IDisposable

    abstract trackref: string

    /// Load the audio clip used for this level
    abstract LoadAudio: unit -> AudioSource

    /// Load the background object used for this level
    abstract LoadBackground: unit -> GameObject

/// Represents a playable track
type public TromboneTrack =
    abstract trackref: string
    abstract trackname_long: string
    abstract trackname_short: string
    abstract year: string
    abstract artist: string
    abstract desc: string
    abstract genre: string
    abstract difficulty: int
    abstract tempo: int
    abstract length: int
    abstract trackindex: int

    /// Called during level loading to load the chart data.
    abstract LoadChart: unit -> SavedLevel

    /// Called during level loading to load assets, such as backgrounds and music.
    abstract LoadTrack: unit -> LoadedTromboneTrack

    /// Whether this track is visible in the track selector
    abstract IsVisible: unit -> bool

/// Ensures track indexes are sequential
type public TrackIndexGenerator() =
    let mutable index = 0

    /// Get the next available track index
    member _.nextIndex () =
        index <- index + 1
        index - 1

/// Track registration callback
type public Callback =
    /// <summary>Called when registering tracks.</summary>
    /// <remarks>You should use the index generator <paramref name="gen" /> for track indexes!</remarks>
    abstract OnRegisterTracks: gen: TrackIndexGenerator -> TromboneTrack seq

/// Track registration event
let EVENT =
    EventFactory.create (fun listeners ->
        { new Callback with
            member _.OnRegisterTracks(gen) =
                listeners |> Seq.collect (fun l -> l.OnRegisterTracks gen) })
