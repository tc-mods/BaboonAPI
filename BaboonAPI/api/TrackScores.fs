namespace BaboonAPI.Hooks.Scores

open System
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

type TrackScoreStorage =
    abstract Priority: int
    abstract CanStore: trackref: string -> bool
    abstract Load: trackref: string -> TrackScore option
    abstract Save: score: TrackScore -> bool
    abstract GetAllScores: unit -> TrackScore seq

module ScoreLookupRegistry =
    let mutable trackScoreStorages: TrackScoreStorage list = []

    let insert (storage: TrackScoreStorage) =
        let index = trackScoreStorages |> List.tryFindIndex (fun st -> storage.Priority < st.Priority)
        trackScoreStorages <- trackScoreStorages |>
        match index with
        | Some i ->
            List.insertAt i storage
        | None ->
            List.append [storage]

    let lookupStorage (trackref: string) =
        trackScoreStorages |> List.tryFind (fun s -> s.CanStore trackref)

    let lookup (trackref: string) =
        trackScoreStorages |> List.tryPick (fun s -> s.Load trackref)

    let AllScores () =
        trackScoreStorages |> Seq.collect (fun s -> s.GetAllScores ())
