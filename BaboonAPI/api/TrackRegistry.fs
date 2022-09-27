module BaboonAPI.Hooks

open BaboonAPI.Event

module Tracks =
    type Callback =
        abstract OnRegisterTracks: int -> SingleTrackData seq

    let EVENT =
        EventFactory.create (fun listeners ->
            let mutable index = 0
            let collector (l: Callback) =
                let s = l.OnRegisterTracks(index) |> Seq.cache
                index <- index + (s |> Seq.length)
                s

            { new Callback with
                member _.OnRegisterTracks(index) =
                    listeners |> Seq.collect collector })

module TrackRegistry =
    let register (track: SingleTrackData) =
        ()
