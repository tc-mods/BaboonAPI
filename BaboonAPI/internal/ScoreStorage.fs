module internal BaboonAPI.Internal.ScoreStorage

open System
open System.IO
open BaboonAPI.Hooks.Saves
open BepInEx.Logging
open Newtonsoft.Json
open UnityEngine

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

    /// Check if this score is empty (no rank and all highscores zero)
    member this.isEmpty =
        this.highestRank.IsNone && Seq.forall (fun i -> i = 0) this.highScores

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

    member this.ToBaseGame () =
        TrackScores(tracktag = this.trackref,
                    letter_grade = rankString this.highestRank,
                    scores = List.toArray this.highScores)

let emptyScore = { trackref = ""; highestRank = None; highScores = [0; 0; 0; 0; 0] }

type IScoreStorage =
    abstract Save : score: AchievedScore -> unit

    abstract Load : trackref: string -> TrackScores

    abstract IsSaved : trackref: string -> bool

[<Serializable>]
[<CLIMutable>]
type SavedScore =
    { [<JsonProperty("rank")>] Rank: string
      [<JsonProperty("highScores")>] HighScores: int list }

type CustomTrackScoreStorage() =
    let mutable scores : Map<string, TrackScores> = Map.empty

    member _.allScores () : TrackScores seq =
        scores.Values

    member _.importScore (ts: TrackScores) =
        scores <- Map.add ts.trackref ts scores

    interface IScoreStorage with
        member this.Load trackref =
            scores
            |> Map.tryFind trackref
            |> Option.defaultWith (fun _ -> { emptyScore with trackref = trackref })

        member this.Save score =
            let current = (this :> IScoreStorage).Load score.trackref

            scores <- Map.add score.trackref (current.updateScore score) scores

        member this.IsSaved trackref =
            scores |> Map.containsKey trackref

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

let customStorage = CustomTrackScoreStorage()

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

    let backupSaveSlot (slot: int) =
        let savePath = $"{Application.persistentDataPath}/tchamp_savev100_{slot}.dat"
        let backupPath = $"{Application.persistentDataPath}/baboonapi_{slot}_backup_firstrun.bak"
        if File.Exists savePath && not (File.Exists backupPath) then
            File.Copy(savePath, backupPath)

    /// Create a first-run backup when a user launches with BaboonAPI installed (or relaunches after making a new save)
    /// Migration code still isn't 100% perfect so this is a backup of old trombloader-era scores
    member this.firstTimeBackup () =
        for saveIndex in [0; 1; 2] do
            backupSaveSlot saveIndex

    /// Old TrombLoader stored custom scores in the basegame array - whereas we put them in a custom storage.
    /// So this function is responsible for removing old entries from the basegame score storage and saving them
    /// in custom storage instead.
    member this.migrateScores () =
        // Find all non-basegame entries
        let scores =
            GlobalVariables.localsave.data_trackscores
            |> Seq.filter (fun s -> s <> null && s[0] <> "" && not (List.contains s[0] trackrefs))
            |> Seq.toList

        // Hotfix for saves broken by 2.1.0
        let isBroken =
            GlobalVariables.localsave.data_trackscores
            |> Array.contains null

        if (not scores.IsEmpty) || isBroken then
            for s in scores do
                let toImport = scoreFromData s

                // Skip empty scores to avoid overwriting good data with bad
                if (not toImport.isEmpty) then
                    customStorage.importScore toImport

            log.LogDebug $"Imported {scores.Length} scores from basegame save"

            // Overwrite basegame array to clean out these entries
            let filtered =
                GlobalVariables.localsave.data_trackscores
                |> Seq.filter (fun s -> s <> null && (List.contains s[0] trackrefs))

            GlobalVariables.localsave.data_trackscores <-
                Seq.append filtered (Seq.init 100 (fun _ -> scoreToData emptyScore))
                |> Seq.take 100
                |> Array.ofSeq

            true
        else
            false

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

        member this.IsSaved trackref =
            Option.isSome (findIndex trackref)

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
