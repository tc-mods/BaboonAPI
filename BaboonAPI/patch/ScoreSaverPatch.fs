namespace BaboonAPI.Patch

open System.Reflection.Emit
open BaboonAPI.Hooks.Scores
open BaboonAPI.Internal
open HarmonyLib
open UnityEngine.UI

type private BaseTrackScore(data: string[]) =
    inherit SimpleTrackScore(data[0], data[2..7] |> Seq.map int |> Seq.toList, rank data[1])

    override this.isBaseGameTrack = true

type private ScoresHelper() =
    static member makeEmptyScore (trackref: string) =
        BaseTrackScore([| trackref; "-"; "0"; "0"; "0"; "0"; "0" |])

    static member ImportScores (trackscores: string[][]): Map<string, TrackScore> =
        let lastIndex = trackscores |> Array.tryFindIndex(fun scores -> isNull scores[0] || scores[0] = "")
        let scores =
            match lastIndex with
            | Some i -> trackscores[..i]
            | None -> trackscores

        scores |> Seq.map(fun s -> s[0], BaseTrackScore s :> TrackScore) |> Map.ofSeq

    // Export scores back into their base game format
    static member ExportScores (scores: TrackScore seq) =
        // Base game expects a 2D array terminated with an uninitialized (all entries null or blank) array
        scores |> Seq.map(fun s ->
            let tss = s.topScores |> List.map(string) |> List.toArray
            Array.append [| s.trackref; s.rankString |] tss
        ) |> Seq.append(seq { yield [| null |] }) |> Seq.toArray

    static member CheckForS (trackref: string) =
        match (ScoreLookupRegistry.lookup trackref) |> Option.bind (fun score -> score.rank) with
        | Some rank -> rank = Rank.S
        | None -> false

    static member PullLetterScore (trackref: string) =
        match ScoreLookupRegistry.lookup trackref with
        | Some score -> score.rankString
        | None -> "-"

    /// Count S ranks... or alternatively just count played tracks
    /// Only use this for showHatchCanvas!
    static member CountSRanks () =
        if GlobalVariables.localsettings.acc_unlockhatches then
            GlobalVariables.localsave.tracks_played / 4
        else
            ScoreLookupRegistry.AllScores()
            |> Seq.filter (fun score -> score.rank |> Option.contains Rank.S)
            |> Seq.length

type private BaseTrackScoreRegistry() =
    let baseGameSongList =
        SongData().data_tracktitles
        |> Seq.map (fun s -> s[2])
        |> Set.ofSeq

    interface TrackScoreStorage with
        member this.GetAllScores() =
            (GlobalVariables.localsave.data_trackscores |> ScoresHelper.ImportScores).Values

        member this.Load trackref =
            GlobalVariables.localsave.data_trackscores
            |> ScoresHelper.ImportScores
            |> Map.tryFind trackref

        member this.Save score =
            // TODO probably do this more efficiently somehow
            if score.isBaseGameTrack then
                GlobalVariables.localsave.data_trackscores <- GlobalVariables.localsave.data_trackscores
                |> ScoresHelper.ImportScores
                |> Map.add score.trackref score
                |> Map.values
                |> ScoresHelper.ExportScores
                true
            else
                false

        member this.CanStore trackref =
            baseGameSongList.Contains trackref

        member this.Priority = 0

exception MissingScoreSaverException of string

[<HarmonyPatch(typeof<SaveSlotController>)>]
type CheckScoresPatch =
    [<HarmonyPrefix>]
    [<HarmonyPatch("checkScores")>]
    static member CheckScores(__instance: SaveSlotController) =
        // Make sure all tracks have score entries
        let missing =
            TrackAccessor.allTracks()
            |> Seq.map(fun rt -> rt.track)
            |> Seq.filter(fun t -> ScoreLookupRegistry.lookup t.trackref |> Option.isNone)

        let mutable shouldSave = false
        for track in missing do
            let ts: TrackScore =
                if track :? BaseGameTrack then
                    ScoresHelper.makeEmptyScore track.trackref
                else
                    SimpleTrackScore track.trackref

            match ScoreLookupRegistry.lookupStorage ts.trackref with
            | Some storage ->
                storage.Save ts |> ignore
                shouldSave <- true
            | None -> raise (MissingScoreSaverException ts.trackref)

        if shouldSave then
            SaverLoader.updateSavedGame()

        false

[<HarmonyPatch(typeof<LevelSelectController>)>]
type LevelSelectPatch =
    [<HarmonyPrefix>]
    [<HarmonyPatch("populateScores")>]
    static member PopulateScores(__instance: SaveSlotController, ___topscores: Text list byref, ___alltrackslist: SingleTrackData list, ___songindex: int) =
        let trackref = ___alltrackslist[___songindex].trackref
        match ScoreLookupRegistry.lookup trackref with
        | Some score ->
            for i, s in score.topScores |> List.indexed do
                ___topscores[i].text <- string s
        | None ->
            ()

        false

    [<HarmonyPrefix>]
    [<HarmonyPatch("checkForS")>]
    static member CheckForS(trackref: string, __result: bool outref) =
        __result <- ScoresHelper.CheckForS trackref
        false

    [<HarmonyPrefix>]
    [<HarmonyPatch("pullLetterScore")>]
    static member PullLetterScore(trackref: string, __result: string outref) =
        __result <- ScoresHelper.PullLetterScore trackref
        false

[<HarmonyPatch(typeof<LatchController>)>]
type LatchControllerPatch =
    [<HarmonyTranspiler>]
    [<HarmonyPatch("showHatchCanvas")>]
    static member ShowHatchCanvas(instructions: CodeInstruction seq): CodeInstruction seq =
        let matcher = CodeMatcher(instructions)
        let endpos = matcher.MatchForward(false, [| CodeMatch(OpCodes.Ldstr, "Player has ") |]).Pos

        matcher
            .Start()
            .RemoveInstructionsInRange(0, endpos - 1)
            .Insert([|
                CodeInstruction.Call(typeof<ScoresHelper>, "CountSRanks")
                CodeInstruction.StoreField(typeof<LatchController>, "num_s")
            |])
            .InstructionEnumeration()

[<HarmonyPatch(typeof<SaverLoader>)>]
type ScoreSaverLoaderPatch =
    [<HarmonyPrefix>]
    [<HarmonyPatch("grabHighestScore")>]
    static member GrabHighestScore(songtag: string, __result: int outref) =
        __result <-
            match ScoreLookupRegistry.lookup songtag with
            | Some score -> List.head score.topScores
            | None -> 0
        false

    [<HarmonyPrefix>]
    [<HarmonyPatch("checkForUpdatedScore")>]
    static member CheckForUpdatedScore(songtag: string, newscore: int, newletterscore: string) =
        match ScoreLookupRegistry.lookup songtag with
        | Some score ->
            rank newletterscore |> Option.iter score.upgradeRank
            score.pushScore newscore
        | None -> ()

        false

module ScoreSaverPatch =
    let setup () =
        ScoreLookupRegistry.insert (BaseTrackScoreRegistry())
