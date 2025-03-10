namespace BaboonAPI.Patch

open BaboonAPI.Internal.ScoreStorage
open HarmonyLib
open UnityEngine

module private TrackScoresUtility =
    let get (trackref: string) = (getStorageFor trackref).Load trackref

    let put (trackref: string) (score: AchievedScore) = (getStorageFor trackref).Save score

    // N.B. basegame usually only returns true if it's rank S, not rank SS
    // so hey, bugfix! now stars should show up on SS-ranked tracks
    let checkForS (trackref: string) =
        match (get trackref).highestRank with
        | Some r -> r >= Rank.S
        | None -> false

    let pullLetterScore (trackref: string) =
        (get trackref).highestRank
        |> rankString

type private TrackScoresAccessor() =
    static member countSRanks () =
        allTrackScores ()
        |> Seq.choose (fun s -> s.highestRank)
        |> Seq.filter (fun r -> r >= Rank.S)
        |> Seq.length

    static member fetchHighScoresFormatted (trackref: string) =
        (TrackScoresUtility.get trackref).highScores
        |> List.map (fun i ->
            if i > 0 then
                i.ToString("n0")
            else
                "-")
        |> ResizeArray
    
    static member fetchHighestScore (trackref: string) =
        (TrackScoresUtility.get trackref).highScores.Head

[<HarmonyPatch>]
type TrackScorePatches() =
    [<HarmonyPrefix>]
    [<HarmonyPatch(typeof<LevelSelectController>, "checkForS")>]
    static member CheckForS(trackData: SingleTrackData, __result: bool outref) =
        __result <- TrackScoresUtility.checkForS trackData.trackref
        false

    [<HarmonyPrefix>]
    [<HarmonyPatch(typeof<LevelSelectController>, "pullLetterScore")>]
    static member PullLetterScore(trackData: SingleTrackData, __result: string outref) =
        __result <- TrackScoresUtility.pullLetterScore trackData.trackref
        false

    [<HarmonyPrefix>]
    [<HarmonyPatch(typeof<SaveSlotController>, "checkScores")>]
    static member SkipCheckingScores() =
        // migrate scores!
        let neededMigration =
            match baseGameStorage with
            | Some bgs -> bgs.migrateScores()
            | None -> false
        
        if neededMigration then
            SaverLoader.updateSavedGame()

        // we don't need to do anything else, our saver will just make things as needed
        false

    [<HarmonyPrefix>]
    [<HarmonyPatch(typeof<SaverLoader>, "grabHighestScore")>]
    static member GrabHighestScore(track: SingleTrackData, __result: int outref) =
        __result <- (TrackScoresUtility.get track.trackref).highScores.Head
        false

    [<HarmonyPrefix>]
    [<HarmonyPatch(typeof<SaverLoader>, "checkForUpdatedScore")>]
    static member UpdateScore(played_track: SingleTrackData, newscore: int, newletterscore: string) =
        let newRank = Rank.from newletterscore
        let score = { trackref = played_track.trackref
                      rank = newRank.Value
                      score = newscore }

        TrackScoresUtility.put played_track.trackref score

        false

    [<HarmonyPrefix>]
    [<HarmonyPatch(typeof<SaverLoader>, "loadAllSaveHighScores")>]
    static member PatchPopulateScores() =
        GlobalVariables.localsave_scores <-
            allTrackScores()
            |> Seq.map _.ToBaseGame()
            |> ResizeArray

        false

    [<HarmonyPrefix>]
    [<HarmonyPatch(typeof<LatchController>, "getNumberOfSScores")>]
    static member PatchLatchCheck(___num_s: int outref) =
        if GlobalVariables.localsettings.acc_unlockhatches then
            ___num_s <- Mathf.FloorToInt((float32 GlobalVariables.localsave.tracks_played) * 0.25f)
        else
            ___num_s <- TrackScoresAccessor.countSRanks()
        false
