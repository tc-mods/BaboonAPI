namespace BaboonAPI.Patch

open System.Collections.Generic
open System.Reflection.Emit
open BaboonAPI.Internal
open BepInEx.Logging
open HarmonyLib
open UnityEngine

type private TrackrefAccessor() =
    static let logger = Logger.CreateLogSource "BaboonAPI.TrackrefAccessor"

    static let makeSongGraph _ =
        Array.init 5 (fun _ -> Random.Range (0, 100))

    static member finalLevelIndex () = TrackAccessor.fetchTrackIndex "einefinal"

    static member trackrefForIndex i = (TrackAccessor.fetchTrackByIndex i).trackref
    static member trackTitleForIndex i = (TrackAccessor.fetchTrackByIndex i).trackname_long
    static member trackDifficultyForIndex i = (TrackAccessor.fetchTrackByIndex i).difficulty
    static member trackLengthForIndex i = (TrackAccessor.fetchTrackByIndex i).length
    static member trackCount () = TrackAccessor.trackCount()

    static member fetchChosenTrack trackref =
        (TrackAccessor.fetchRegisteredTrack trackref).asTrackData

    static member doLevelSelectStart (instance: LevelSelectController, alltrackslist: List<SingleTrackData>) =
        try
            TrackAccessor.allTracks()
            |> Seq.filter (fun s -> s.track.IsVisible())
            |> Seq.map (fun s -> s.asTrackData)
            |> alltrackslist.AddRange
        with
        | TrackAccessor.DuplicateTrackrefException trackref ->
            // TODO: Show an error popup in game? The game doesn't have anything for this...
            logger.LogFatal $"Duplicate trackref {trackref}, songs not loading!"

    static member populateSongGraphs () =
        Array.init (TrackAccessor.trackCount()) makeSongGraph

[<HarmonyPatch(typeof<SaverLoader>, "loadTrackData")>]
type TrackLoaderPatch() =
    // Patches the track lookup function to use our registry :)
    static member Prefix (trackref: string) =
        GlobalVariables.chosen_track <- trackref
        GlobalVariables.chosen_track_data <- TrackrefAccessor.fetchChosenTrack trackref
        false

[<HarmonyPatch>]
type TrackTitlePatches() =
    static let tracktitles_f = AccessTools.Field(typeof<GlobalVariables>, nameof GlobalVariables.data_tracktitles)

    [<HarmonyTranspiler>]
    [<HarmonyPatch(typeof<LevelSelectController>, "Start")>]
    static member Transpiler(instructions: CodeInstruction seq): CodeInstruction seq =
        let matcher =
            CodeMatcher(instructions)
                .MatchForward(false, [|
                    CodeMatch (fun ins -> ins.LoadsConstant(0L))
                    CodeMatch OpCodes.Stloc_3
                |])
                .ThrowIfInvalid("Could not find start of injection point in LevelSelectController#Start")

        let start = matcher.Pos

        matcher
            .MatchForward(true, [|
                CodeMatch(fun ins -> ins.LoadsField(tracktitles_f))
                CodeMatch OpCodes.Ldlen
            |])
            .ThrowIfInvalid("Could not find end of injection point in LevelSelectController#Start")
            .RemoveInstructionsInRange(start, matcher.Pos + 2) // Remove the for loop
            .Start()
            .Advance(start) // Go to where the for loop used to be
            .InsertAndAdvance([|
                CodeInstruction OpCodes.Ldarg_0
                CodeInstruction OpCodes.Ldarg_0
                CodeInstruction.LoadField(typeof<LevelSelectController>, "alltrackslist")
                CodeInstruction.Call(typeof<TrackrefAccessor>, "doLevelSelectStart",
                               [| typeof<LevelSelectController>; typeof<List<SingleTrackData>> |])

                // populate song graphs
                CodeInstruction OpCodes.Ldarg_0
                CodeInstruction.Call(typeof<TrackrefAccessor>, "populateSongGraphs")
                CodeInstruction.StoreField(typeof<LevelSelectController>, "songgraphs")
            |])
            .MatchForward(false, [|
                CodeMatch(fun ins -> ins.LoadsField(tracktitles_f))
                CodeMatch OpCodes.Ldlen
            |])
            .ThrowIfInvalid("Could not find data_tracktitles length lookup in LevelSelectController#Start")
            .SetInstructionAndAdvance(CodeInstruction OpCodes.Ldc_I4_0) // "j < 0"; set for loop to not iterate
            .RemoveInstruction() // remove ldlen
            .InstructionEnumeration()
