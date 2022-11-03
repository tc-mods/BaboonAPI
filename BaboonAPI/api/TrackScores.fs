namespace BaboonAPI.Hooks.Scores

open System
open BaboonAPI.Event

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

[<AbstractClass>]
type TrackScore() =
    abstract trackref: string
    abstract rank: Rank option
    abstract topScores: int list

    member this.rankString =
        match this.rank with
        | Some r -> LanguagePrimitives.EnumToValue r |> Char.ToString
        | None -> "_"

type MutableTrackScore(trackref: string) =
    inherit TrackScore()

    let mutable highscores = [0; 0; 0; 0; 0]
    let mutable highestRank: Rank option = None

    override _.trackref = trackref

    override _.rank = highestRank

    member _.setRank rank =
        highestRank <- Some rank

    /// Get the 5 top scores
    override _.topScores = highscores

    /// Push a new score into the highscore list, sorting it correctly
    member _.pushScore score =
        highscores <- (score :: highscores |> List.sortDescending |> List.take 5)

module ScoreLookupRegistry =
    type Listener =
        abstract Lookup: trackref: string -> TrackScore option
        abstract AllScores: unit -> TrackScore seq

    let EVENT = EventFactory<Listener>.create(fun listeners ->
        { new Listener with
            member _.Lookup trackref =
                listeners |> Seq.map(fun l -> l.Lookup trackref) |> Seq.tryFind(fun it -> it.IsSome) |> Option.flatten

            member _.AllScores () =
                listeners |> Seq.collect(fun l -> l.AllScores()) })

    let lookup trackref = EVENT.invoker.Lookup trackref
