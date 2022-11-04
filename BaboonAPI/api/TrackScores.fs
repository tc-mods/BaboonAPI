namespace BaboonAPI.Hooks.Scores

open System
open BaboonAPI.Event
open Microsoft.FSharp.Core
type Rank =
    | S = 'S'
    | A = 'A'
    | B = 'B'
    | C = 'C'
    | D = 'D'
    | E = 'E'
    | F = 'F'

[<AutoOpen>]
module RankExtension =
    let rank (name: string): Rank option =
        if name = "-" then
            None
        else
            Some(LanguagePrimitives.EnumOfValue name[0])

    let rankOrdinal (rank: Rank) =
        match rank with
        | Rank.S -> 10
        | Rank.A -> 5
        | Rank.B -> 4
        | Rank.C -> 3
        | Rank.D -> 2
        | Rank.E -> 1
        | Rank.F -> 0
        | _ -> -1

[<AbstractClass>]
type TrackScore(trackref: string) =
    abstract rank: Rank option
    abstract topScores: int list

    abstract upgradeRank: rank: Rank -> unit

    abstract pushScore: score: int -> unit

    abstract isBaseGameTrack: bool

    member _.trackref = trackref

    member this.rankString =
        match this.rank with
        | Some r -> LanguagePrimitives.EnumToValue r |> Char.ToString
        | None -> "_"

type SimpleTrackScore(trackref: string, highscores: int list, highestRank: Rank option) =
    inherit TrackScore(trackref)

    let mutable highscores = highscores
    let mutable highestRank: Rank option = highestRank

    new(trackref: string) =
        SimpleTrackScore(trackref, [0; 0; 0; 0; 0], None)

    override _.rank = highestRank

    override _.upgradeRank newRank =
        match highestRank with
        | Some oldRank ->
            if rankOrdinal newRank > rankOrdinal oldRank then
                highestRank <- Some newRank
        | None ->
            highestRank <- Some newRank

    /// Get the 5 top scores
    override _.topScores = highscores

    /// Push a new score into the highscore list, sorting it correctly
    override _.pushScore score =
        highscores <- (score :: highscores |> List.sortDescending |> List.take 5)

    override this.isBaseGameTrack = false

module ScoreLookupRegistry =
    type public Listener =
        abstract Lookup: trackref: string -> TrackScore option
        abstract Save: score: TrackScore -> bool
        abstract AllScores: unit -> TrackScore seq

    let EVENT = EventFactory<Listener>.create(fun listeners ->
        { new Listener with
            member _.Lookup trackref =
                listeners |> Seq.map(fun l -> l.Lookup trackref) |> Seq.tryFind(fun it -> it.IsSome) |> Option.flatten

            member _.Save score =
                listeners |> Seq.tryFind(fun l -> l.Save score) |> Option.isSome

            member _.AllScores () =
                listeners |> Seq.collect(fun l -> l.AllScores()) })

    let lookup trackref = EVENT.invoker.Lookup trackref
