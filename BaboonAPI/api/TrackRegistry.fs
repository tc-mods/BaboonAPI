﻿/// <summary>
/// API for registering new tracks.
/// </summary>
/// <remarks>
/// <code>open BaboonAPI.Hooks
///
///member _.Awake () =
///    Tracks.EVENT.Register(MyTrackRegistrationListener())
/// </code>
/// </remarks>
module BaboonAPI.Hooks.Tracks

open System
open BaboonAPI.Event
open UnityEngine

type public LoadedTromboneTrack =
    inherit IDisposable

    abstract trackref: string
    abstract LoadAudio: unit -> AudioSource
    abstract LoadBackground: unit -> GameObject

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

type public TrackIndexGenerator() =
    let mutable index = 0

    member _.nextIndex () =
        index <- index + 1
        index - 1

type public Callback =
    abstract OnRegisterTracks: TrackIndexGenerator -> TromboneTrack seq

let EVENT =
    EventFactory.create (fun listeners ->
        { new Callback with
            member _.OnRegisterTracks(gen) =
                listeners |> Seq.collect (fun l -> l.OnRegisterTracks gen) })
