namespace BaboonAPI.Hooks.Tracks

open System
open BaboonAPI.Event
open BaboonAPI.Utility.Coroutines
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

/// Context for pausing & resuming
type public PauseContext (controller: PauseCanvasController) =
    /// Game controller for this track
    member _.gameController = controller.gc

    /// Background controller
    member _.backgroundController = controller.gc.bgcontroller

    /// Current full background object
    member _.backgroundObj = controller.gc.bgcontroller.fullbgobject

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
    abstract OnPause: PauseContext -> unit

    /// Called when this track is resumed (after the countdown).
    abstract OnResume: PauseContext -> unit

/// LoadedTromboneTrack extension to support async audio loading
type public AsyncAudioAware =
    /// Load the audio clip for this song asynchronously
    abstract LoadAudio: unit -> YieldTask<Result<TrackAudio, string>>

/// TromboneTrack extension for preview clips
type public Previewable =
    /// <summary>Called when attempting to load a clip for preview.</summary>
    /// <remarks>
    /// The implementation should return a valid audio clip on successful load.
    /// The implementation may return a Error result with an error message if loading fails for any reason.
    /// </remarks>
    abstract LoadClip: unit -> YieldTask<Result<TrackAudio, string>>

/// TromboneTrack extension to indicate sortability
type public Sortable =
    /// Sort order for this track
    abstract sortOrder: int

/// TromboneTrack extension to indicate a track loaded from the local filesystem
type public FilesystemTrack =
    /// The full path to the local filesystem folder this track is stored in
    abstract folderPath: string

/// Represents a song graph
type public SongGraph =
    { fury: int
      tears: int
      spunk: int
      doots: int
      slides: int }

    /// <summary>Create a graph with all values set to <paramref name="value" /></summary>
    static member all (value: int) =
        { fury = value; tears = value; spunk = value; doots = value; slides = value; }

    /// Get the graph as a 5-tuple of integers for passing back to the game
    member public this.asArray =
        [| this.fury; this.tears; this.spunk; this.doots; this.slides |]

/// TromboneTrack extension for drawing song graphs on the track select screen
type public Graphable =
    /// <summary>Draw a song graph for this track</summary>
    /// <remarks>Implementations should return None if they don't have a graph</remarks>
    abstract CreateGraph: unit -> SongGraph option

/// Track collection interface
type TromboneCollection =
    /// The tracks included in this collection
    abstract tracks: TromboneTrack seq

    /// Unique ID for this collection
    abstract unique_id: string

    /// Player-facing name of this collection
    abstract name: string

    /// Player-facing description of this collection
    abstract description: string

    /// Resolve this collection into a concrete collection
    abstract Resolve: index: int -> YieldTask<TrackCollection>

type ProgressUpdate =
    { loaded: int }

type TracksLoadedInfo =
    { totalTracks: int
      totalCollections: int }

/// Current track loading progress
type Progress =
    | LoadingTracks of ProgressUpdate
    | LoadingCollections of ProgressUpdate
    | Done of TracksLoadedInfo

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

/// Event fired whenever tracks have finished loading.
module TracksLoadedEvent =
    /// Event listener type
    type public Listener =
        /// Called when tracks have finished loading.
        abstract OnTracksLoaded: TromboneTrack list -> unit

    /// Event bus
    let EVENT =
        EventFactory.create (fun listeners ->
            { new Listener with
                member _.OnTracksLoaded tracks =
                    listeners |> Seq.iter (fun l -> l.OnTracksLoaded tracks) })

/// Event fired to build collections
module TrackCollectionRegistrationEvent =
    type public Listener =
        abstract OnRegisterCollections: unit -> TromboneCollection seq

    /// Event bus
    let EVENT =
        EventFactory.create (fun listeners ->
            { new Listener with
                member _.OnRegisterCollections () =
                    listeners |> Seq.collect (_.OnRegisterCollections()) })
