namespace BaboonAPI.Patch

open System.Collections.Generic
open System.Reflection.Emit
open BaboonAPI.Hooks.Tracks
open BaboonAPI.Internal
open HarmonyLib

type private TrackrefAccessor() =
    static let makeSingleTrackData (track: TromboneTrack) =
        let data = SingleTrackData()
        data.trackname_long <- track.trackname_long
        data.trackname_short <- track.trackname_short
        data.trackindex <- track.trackindex
        data.artist <- track.artist
        data.year <- track.year
        data.desc <- track.desc
        data.difficulty <- track.difficulty
        data.genre <- track.genre
        data.length <- track.length
        data.tempo <- track.tempo
        data.trackref <- track.trackref

        if track.IsVisible() then
            Some data
        else
            None

    static member finalLevelIndex () =
        (TrackAccessor.fetchTrack "einefinal").trackindex

    static member trackrefForIndex i = (TrackAccessor.fetchTrackByIndex i).trackref
    static member trackTitleForIndex i = (TrackAccessor.fetchTrackByIndex i).trackname_long
    static member trackDifficultyForIndex i = (TrackAccessor.fetchTrackByIndex i).difficulty
    static member trackLengthForIndex i = (TrackAccessor.fetchTrackByIndex i).length

    static member doLevelSelectStart (instance: LevelSelectController, alltrackslist: List<SingleTrackData>) =
        instance.sortdrop.SetActive false
        TrackAccessor.allTracks()
        |> Seq.choose makeSingleTrackData
        |> alltrackslist.AddRange

[<HarmonyPatch>]
type FinalLevelPatches() =
    static let trackrefs_f =
        AccessTools.Field(typeof<GlobalVariables>, nameof GlobalVariables.data_trackrefs)

    /// Final level check usually checks against track index, which is dumb.
    /// This transpiler fixes that by returning the actual index of "einefinal", the final level.
    [<HarmonyTranspiler>]
    [<HarmonyPatch(typeof<CurtainController>, "loadNextScene")>]
    [<HarmonyPatch(typeof<LockController>, "loadCharSelect")>]
    static member Transpiler(instructions: CodeInstruction seq) : CodeInstruction seq = seq {
        use iter = instructions.GetEnumerator()

        while iter.MoveNext() do
            let ins = iter.Current

            if ins.LoadsField(trackrefs_f) then
                yield CodeInstruction.Call(typeof<TrackrefAccessor>, "finalLevelIndex")

                // skip the next 4 instructions, usually used for loading length
                iter.MoveNext() |> ignore // IL_0092: ldlen
                iter.MoveNext() |> ignore // IL_0093: conv.i4
                iter.MoveNext() |> ignore // IL_0094: ldc.i4.1
                iter.MoveNext() |> ignore // IL_0095: sub
            else
                yield ins
    }

[<HarmonyPatch>]
type TrackRefPatches() =
    static let trackrefs_f =
        AccessTools.Field(typeof<GlobalVariables>, nameof (GlobalVariables.data_trackrefs))

    /// Patch various GlobalVariable lookups of trackrefs to use our track registry instead
    [<HarmonyTranspiler>]
    [<HarmonyPatch(typeof<PointSceneController>, "Start")>]
    [<HarmonyPatch(typeof<PointSceneController>, "updateSave")>]
    [<HarmonyPatch(typeof<GameController>, "buildLevel")>]
    static member Transpiler(instructions: CodeInstruction seq) : CodeInstruction seq =
        let matcher = CodeMatcher(instructions).MatchForward(false, [|
            CodeMatch(fun ins -> ins.LoadsField(trackrefs_f))
            CodeMatch() // match anything
            CodeMatch(OpCodes.Ldelem_Ref)
        |])

        matcher.Repeat(fun matcher ->
            let lf_labels = matcher.Labels
            matcher.RemoveInstruction()
                .AddLabels(lf_labels) // shuffle labels onto new start
                .Advance(1) // skip the load
                .SetInstruction(CodeInstruction.Call(typeof<TrackrefAccessor>, "trackrefForIndex", [| typeof<int> |]))
                |> ignore
        ).InstructionEnumeration()

[<HarmonyPatch>]
type TrackTitlePatches() =
    static let tracktitles_f = AccessTools.Field(typeof<GlobalVariables>, nameof GlobalVariables.data_tracktitles)

    [<HarmonyTranspiler>]
    [<HarmonyPatch(typeof<LevelSelectController>, "Start")>]
    static member Transpiler(instructions: CodeInstruction seq): CodeInstruction seq =
        let matcher = CodeMatcher(instructions).MatchForward(true, [|
            CodeMatch(fun ins -> ins.LoadsField(tracktitles_f))
            CodeMatch OpCodes.Ldlen
        |])

        matcher
            .RemoveInstructionsInRange(0, matcher.Pos + 2) // Remove the whole start of the method
            .Start() // Back to the beginning, insert our own call instead
            .InsertAndAdvance([|
                CodeInstruction OpCodes.Ldarg_0
                CodeInstruction OpCodes.Ldarg_0
                CodeInstruction.LoadField(typeof<LevelSelectController>, "alltrackslist")
                CodeInstruction.Call(typeof<TrackrefAccessor>, "doLevelSelectStart",
                               [| typeof<LevelSelectController>; typeof<List<SingleTrackData>> |])
            |])
            .InstructionEnumeration()
    
    [<HarmonyTranspiler>]
    [<HarmonyPatch(typeof<PointSceneController>, "Start")>]
    [<HarmonyPatch(typeof<PointSceneController>, "getTootsNum")>]
    static member PatchTitleLookup(instructions: CodeInstruction seq): CodeInstruction seq =
        CodeMatcher(instructions)
            .MatchForward(false, [|
                CodeMatch(fun ins -> ins.LoadsField(tracktitles_f))
                CodeMatch OpCodes.Ldsfld
                CodeMatch OpCodes.Ldelem_Ref
            |])
            .Repeat(fun matcher ->
                let methodName, removeCount =
                    match matcher.InstructionAt(3).opcode with
                    | op when op = OpCodes.Ldc_I4_0 -> "trackTitleForIndex", 3
                    | op when op = OpCodes.Ldc_I4_6 -> "trackDifficultyForIndex", 4
                    | op when op = OpCodes.Ldc_I4_7 -> "trackLengthForIndex", 4
                    | _ -> failwith "unknown opcode for data_tracktitles patch"

                matcher
                    .RemoveInstruction() // remove ldsfld
                    .Advance(1) // keep the ldsfld for trackindex
                    .RemoveInstructions(removeCount) // remove array indexing & int parsing...
                    .Insert([|
                        CodeInstruction.Call(typeof<TrackrefAccessor>, methodName, [| typeof<int> |])
                    |]) |> ignore
            )
            .InstructionEnumeration()
