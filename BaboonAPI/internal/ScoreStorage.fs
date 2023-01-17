module internal BaboonAPI.Internal.ScoreStorage

open System
open BaboonAPI.Hooks.Saves
open BepInEx.Logging
open Newtonsoft.Json

let log = Logger.CreateLogSource "BaboonAPI.ScoreStorage"

type Rank =
    | F
    | D
    | C
    | B
    | A
    | S
    | SS

    override this.ToString() =
        match this with
        | SS -> "SS"
        | S -> "S"
        | A -> "A"
        | B -> "B"
        | C -> "C"
        | D -> "D"
        | F -> "F"

    static member from (s: string) =
        match s with
        | "SS" -> Some SS
        | "S" -> Some S
        | "A" -> Some A
        | "B" -> Some B
        | "C" -> Some C
        | "D" -> Some D
        | "F" -> Some F
        | _ -> None

let rankString (r: Rank option) =
    match r with
    | Some r -> r.ToString()
    | None -> "-"

type AchievedScore =
   { trackref: string
     rank: Rank
     score: int }

type TrackScores =
    { trackref: string
      highestRank: Rank option
      highScores: int list }

    member this.updateScore (achieved: AchievedScore) =
        let highestRank =
            match this.highestRank with
            | None -> Some achieved.rank
            | Some old when achieved.rank > old -> Some achieved.rank
            | _ -> this.highestRank

        let highScores =
            achieved.score :: this.highScores
            |> List.sortDescending
            |> List.take 5

        { this with highestRank = highestRank
                    highScores = highScores }

let emptyScore = { trackref = ""; highestRank = None; highScores = [0; 0; 0; 0; 0] }

type IScoreStorage =
    abstract Save : score: AchievedScore -> unit

    abstract Load : trackref: string -> TrackScores

[<Serializable>]
[<CLIMutable>]
type SavedScore =
    { [<JsonProperty("rank")>] Rank: string
      [<JsonProperty("highScores")>] HighScores: int list }

type CustomTrackScoreStorage() =
    let mutable scores : Map<string, TrackScores> = Map.empty

    member _.allScores () : TrackScores seq =
        scores.Values

    interface IScoreStorage with
        member this.Load trackref =
            scores
            |> Map.tryFind trackref
            |> Option.defaultWith (fun _ -> { emptyScore with trackref = trackref })

        member this.Save score =
            let current = (this :> IScoreStorage).Load score.trackref

            scores <- Map.add score.trackref (current.updateScore score) scores

    interface ICustomSaveData<Map<string, SavedScore>> with
        member this.Convert o = o.ToObject()

        member this.Load disk =
            scores <- disk
            |> Map.map (fun trackref data -> { trackref = trackref
                                               highestRank = Rank.from data.Rank
                                               highScores = data.HighScores })

        member this.Save() =
            scores
            |> Map.map (fun _ score -> { Rank = rankString score.highestRank
                                         HighScores = score.highScores })

type BaseTrackScoreStorage(trackrefs: string list) =
    let scoreFromData (data: string[]) =
        { trackref = data[0]
          highestRank = (Rank.from data[1])
          highScores = (data[2..7] |> Seq.map int |> Seq.toList) }

    let scoreToData (scores: TrackScores) =
        (Seq.initInfinite (fun _ -> 0))
        |> Seq.append scores.highScores
        |> Seq.take 5
        |> Seq.map string
        |> Seq.toArray
        |> Array.append [| scores.trackref; (rankString scores.highestRank) |> string |]

    let findIndex (trackref: string) =
        GlobalVariables.localsave.data_trackscores
        |> Seq.takeWhile (isNull >> not) // non-null...
        |> Seq.takeWhile (fun s -> s[0] <> "") // with non-empty trackref
        |> Seq.tryFindIndex (fun s -> s[0] = trackref)

    let findEmptySlot () =
        GlobalVariables.localsave.data_trackscores
        |> Seq.tryFindIndex (fun s -> s = null || s[0] = "")

    member _.canStore (trackref: string) =
        trackrefs |> List.contains trackref

    member _.allScores () : TrackScores seq =
        GlobalVariables.localsave.data_trackscores
        |> Seq.takeWhile (isNull >> not) // non-null...
        |> Seq.takeWhile (fun s -> s[0] <> "") // with non-empty trackref
        |> Seq.map scoreFromData

    interface IScoreStorage with
        member this.Load trackref =
            match findIndex trackref with
            | Some i ->
                scoreFromData GlobalVariables.localsave.data_trackscores[i]
            | None ->
                { emptyScore with trackref = trackref }

        member this.Save score =
            let index = findIndex score.trackref
            let current =
                match index with
                | Some i ->
                    scoreFromData GlobalVariables.localsave.data_trackscores[i]
                | None ->
                    { emptyScore with trackref = score.trackref }

            let updated = current.updateScore score

            match index with
            | Some i ->
                GlobalVariables.localsave.data_trackscores[i] <- scoreToData updated
            | None ->
                match findEmptySlot() with
                | Some j ->
                    GlobalVariables.localsave.data_trackscores[j] <- scoreToData updated
                | None ->
                    // can't save, no space left in array!
                    log.LogWarning $"Dropping score data for track {score.trackref} as the base game array is full!"
                    ()

            ()

let customStorage = CustomTrackScoreStorage()
let mutable baseGameStorage = None

let initialize (trackrefs: string list) =
    baseGameStorage <- Some (BaseTrackScoreStorage trackrefs)

// If base game storage is initialized and can store the score, use that
// Otherwise use our custom storage
let getStorageFor (trackref: string) : IScoreStorage =
    match baseGameStorage with
    | Some bgs when bgs.canStore trackref -> bgs
    | _ -> customStorage

let allTrackScores () =
    let baseGameTracks =
        match baseGameStorage with
        | Some bgs -> bgs.allScores()
        | None -> Seq.empty

    Seq.append baseGameTracks (customStorage.allScores ())
