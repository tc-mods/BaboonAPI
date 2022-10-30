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

/// Loaded audio clip & volume
type public TrackAudio =
    { Clip: AudioClip
      Volume: float32 }
    
/// Context passed to LoadBackground callback
type public BackgroundContext(controller: GameController) =
    /// Game controller that is currently attempting to load this background
    member _.controller = controller

/// Loaded track assets, disposed when a level ends
type public LoadedTromboneTrack =
    inherit IDisposable

    abstract trackref: string

    /// Load the audio clip used for this level
    abstract LoadAudio: unit -> TrackAudio

    /// Load the background object used for this level
    abstract LoadBackground: ctx: BackgroundContext -> GameObject

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

    /// Called during level loading to load the chart data.
    abstract LoadChart: unit -> SavedLevel

    /// Called during level loading to load assets, such as backgrounds and music.
    abstract LoadTrack: unit -> LoadedTromboneTrack

    /// Whether this track is visible in the track selector
    abstract IsVisible: unit -> bool

/// Track registration callback
type public Callback =
    /// <summary>Called when registering tracks.</summary>
    abstract OnRegisterTracks: unit -> TromboneTrack seq

/// Track registration event
let EVENT =
    EventFactory.create (fun listeners ->
        { new Callback with
            member _.OnRegisterTracks () =
                listeners |> Seq.collect (fun l -> l.OnRegisterTracks()) })
