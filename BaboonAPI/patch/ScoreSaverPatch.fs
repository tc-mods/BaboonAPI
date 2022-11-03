namespace BaboonAPI.Patch

open System.Reflection.Emit
open BaboonAPI.Hooks.Scores
open BaboonAPI.Internal
open BaboonAPI.Patch
open HarmonyLib
open UnityEngine.UI

type private BaseTrackScore(data: string[]) =
    inherit TrackScore()

    let mappedScores = data[2..6] |> Seq.map int

    override this.topScores = List.ofSeq mappedScores
    override this.trackref = data[0]
    override this.rank = rank data[1]

type private BaseTrackScoreRegistry() =
    interface ScoreLookupRegistry.Listener with
        member this.AllScores() =
            GlobalVariables.localsave.data_trackscores |> Seq.map(fun data -> BaseTrackScore data)

        member this.Lookup(trackref) = failwith "todo"

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

    static member CountSRanks () =
        ScoreLookupRegistry.EVENT.invoker.AllScores()
        |> Seq.filter (fun score -> score.rank |> Option.contains Rank.S)
        |> Seq.length

[<HarmonyPatch(typeof<SaveSlotController>)>]
type CheckScoresPatch =
    [<HarmonyPrefix>]
    [<HarmonyPatch("checkScores")>]
    static member CheckScores(__instance: SaveSlotController) =
        // Just patch this method to be less bad
        let mutable scores = ScoresHelper.ImportScores GlobalVariables.localsave.data_trackscores

        // Only pick base game tracks here. Everything else will get saved to our custom save
        let missing =
            TrackAccessor.allTracks()
            |> Seq.map(fun rt -> rt.track)
            |> Seq.filter(fun t -> t :? BaseGameTrack && not (scores.ContainsKey t.trackref))
        let mutable shouldSave = false
        for track in missing do
            scores <- scores.Add (track.trackref, ScoresHelper.makeEmptyScore track.trackref)
            shouldSave <- true

        if shouldSave then
            GlobalVariables.localsave.data_trackscores <- ScoresHelper.ExportScores scores.Values
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
type SaverLoaderPatch =
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
            // TODO mutate the saved score
            ()
        | None -> ()

        false
