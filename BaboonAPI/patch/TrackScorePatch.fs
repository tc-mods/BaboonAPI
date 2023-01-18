namespace BaboonAPI.Patch

open System
open System.Reflection.Emit
open BaboonAPI.Internal.ScoreStorage
open HarmonyLib

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

    static member fetchHighScores (trackref: string) =
        (TrackScoresUtility.get trackref).highScores
        |> List.map (fun i ->
            if i > 0 then
                i.ToString("n0")
            else
                "-")
        |> ResizeArray

[<HarmonyPatch>]
type TrackScorePatches() =
    [<HarmonyPrefix>]
    [<HarmonyPatch(typeof<LevelSelectController>, "checkForS")>]
    static member CheckForS(tag: string, __result: bool outref) =
        __result <- TrackScoresUtility.checkForS tag
        false

    [<HarmonyPrefix>]
    [<HarmonyPatch(typeof<LevelSelectController>, "pullLetterScore")>]
    static member PullLetterScore(tag: string, __result: string outref) =
        __result <- TrackScoresUtility.pullLetterScore tag
        false

    [<HarmonyPrefix>]
    [<HarmonyPatch(typeof<SaveSlotController>, "checkScores")>]
    static member SkipCheckingScores() =
        // we don't need to do this, our saver will just make things as needed
        false

    [<HarmonyPrefix>]
    [<HarmonyPatch(typeof<SaverLoader>, "grabHighestScore")>]
    static member GrabHighestScore(songtag: string, __result: int outref) =
        __result <- (TrackScoresUtility.get songtag).highScores.Head
        false

    [<HarmonyPrefix>]
    [<HarmonyPatch(typeof<SaverLoader>, "checkForUpdatedScore")>]
    static member UpdateScore(songtag: string, newscore: int, newletterscore: string) =
        let newRank = Rank.from newletterscore
        let score = { trackref = songtag
                      rank = newRank.Value
                      score = newscore }

        TrackScoresUtility.put songtag score

        false

    [<HarmonyTranspiler>]
    [<HarmonyPatch(typeof<LevelSelectController>, "populateScores")>]
    static member PatchPopulateScores(instructions: CodeInstruction seq): CodeInstruction seq =
        let matcher = CodeMatcher(instructions)

        let startIndex =
            matcher.MatchForward(false, [|
                CodeMatch (fun ins -> ins.LoadsConstant 0L)
                CodeMatch OpCodes.Stloc_2
                CodeMatch OpCodes.Br
            |]).ThrowIfInvalid("Could not find start of for loop in LevelSelectController#populateScores").Pos

        let endIndex =
            matcher.MatchForward(true, [|
                CodeMatch OpCodes.Ldloc_3
                CodeMatch (fun ins -> ins.LoadsConstant 7L)
                CodeMatch OpCodes.Blt
            |]).ThrowIfInvalid("Could not find end of 2nd for loop in LevelSelectController#populateScores").Pos

        matcher
            .RemoveInstructionsInRange(startIndex, endIndex)
            .Start()
            .Advance(startIndex)
            .InsertAndAdvance([|
                CodeInstruction OpCodes.Ldarg_0
                CodeInstruction.LoadField(typeof<LevelSelectController>, "alltrackslist")
                CodeInstruction OpCodes.Ldarg_0
                CodeInstruction.LoadField(typeof<LevelSelectController>, "songindex")
                CodeInstruction.Call(typeof<ResizeArray<SingleTrackData>>, "get_Item", [| typeof<int32> |])
                CodeInstruction.LoadField(typeof<SingleTrackData>, "trackref")
                CodeInstruction.Call(typeof<TrackScoresAccessor>, "fetchHighScores")
                CodeInstruction OpCodes.Dup
                CodeInstruction OpCodes.Stloc_1
                CodeInstruction OpCodes.Ldc_I4_0
                CodeInstruction.Call(typeof<ResizeArray<string>>, "get_Item", [| typeof<int32> |])
                CodeInstruction.Call(typeof<Int32>, "Parse", [| typeof<string> |])
                CodeInstruction.StoreField(typeof<LevelSelectController>, "highestscore")
            |])
            .InstructionEnumeration()

    [<HarmonyTranspiler>]
    [<HarmonyPatch(typeof<LatchController>, "showHatchCanvas")>]
    static member PatchHatchCanvas(instructions: CodeInstruction seq) : CodeInstruction seq =
        let matcher = CodeMatcher(instructions)

        let startIndex =
            matcher.MatchForward(true, [|
                CodeMatch (fun ins -> ins.LoadsConstant(0L))
                CodeMatch OpCodes.Stloc_0
                CodeMatch OpCodes.Br
            |]).ThrowIfInvalid("Could not find start of for loop in LatchController#showHatchCanvas").Pos

        let endIndex =
            matcher.MatchForward(true, [|
                CodeMatch OpCodes.Ldlen
                CodeMatch OpCodes.Conv_I4
                CodeMatch OpCodes.Blt
            |]).ThrowIfInvalid("Could not find end of for loop in LatchController#showHatchCanvas").Pos

        matcher
            .RemoveInstructionsInRange(startIndex, endIndex)
            .Start()
            .Advance(startIndex)
            .InsertAndAdvance([|
                CodeInstruction OpCodes.Ldarg_0
                CodeInstruction.Call(typeof<TrackScoresAccessor>, "countSRanks")
                CodeInstruction.StoreField(typeof<LatchController>, "num_s")
            |])
            .InstructionEnumeration()
