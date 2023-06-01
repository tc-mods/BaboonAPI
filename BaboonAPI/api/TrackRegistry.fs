namespace BaboonAPI.Hooks.Tracks

open System
open BaboonAPI.Event
open UnityEngine

/// <namespacedoc>
/// <summary>
/// Track registration &amp; loading APIs
/// </summary>
/// <remarks>
/// Provides hooks for registering tracks, plus loading custom charts, audio &amp; backgrounds.
/// </remarks>
/// </namespacedoc>
///
/// <summary>Loaded audio clip &amp; volume</summary>
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

    /// Delayed background setup hook; use if you want to modify the background after it's been cloned.
    abstract SetUpBackgroundDelayed: controller: BGController -> bg: GameObject -> unit

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

/// <summary>
/// LoadedTromboneTrack extension for pause/resume functionality
/// </summary>
/// <remarks>
/// Implementing this class allows you to react to the pause and resume 
/// </remarks>
type public PauseAware =
    /// <summary>Can this track be resumed after a pause?</summary>
    /// <remarks>If false, the curtains will close on pause.</remarks>
    abstract CanResume: bool

    /// Called when this track is paused. Use this to pause backgrounds or other features.
    abstract OnPause: unit -> unit

    /// Called when this track is resumed (after the countdown).
    abstract OnResume: unit -> unit

/// <summary>
/// Event-based API for registering new tracks.
/// </summary>
/// <example>
/// <code>open BaboonAPI.Hooks.Tracks
///
///member _.Awake () =
///    TrackRegistrationEvent.EVENT.Register MyTrackRegistrationListener()
/// </code>
/// </example>
module TrackRegistrationEvent =
    /// Track registration listener
    type public Listener =
        /// <summary>Called when registering tracks.</summary>
        abstract OnRegisterTracks: unit -> TromboneTrack seq

    /// Event bus
    let EVENT =
        EventFactory.create (fun listeners ->
            { new Listener with
                member _.OnRegisterTracks () =
                    listeners |> Seq.collect (fun l -> l.OnRegisterTracks()) })
