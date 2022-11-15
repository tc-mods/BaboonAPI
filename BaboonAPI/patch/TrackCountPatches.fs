namespace BaboonAPI.Patch

open System.Reflection.Emit
open BaboonAPI.Internal
open HarmonyLib

type private TrackCountAccessor() =
    static member trackCount () = TrackAccessor.trackCount()

    static member trackrefByIndex i = TrackAccessor.fetchTrackByIndex i |> (fun t -> t.trackref)

[<HarmonyPatch>]
type TrackCountPatches() =
    static let tracktitles_f =
        AccessTools.Field(typeof<GlobalVariables>, nameof GlobalVariables.data_tracktitles)
    static let progression_champ_f =
        AccessTools.Field(typeof<SavedCardCollection>, "progression_trombone_champ")

    /// Patches anywhere that reads the trackref length and replaces it with the "true" track count
    [<HarmonyTranspiler>]
    [<HarmonyPatch(typeof<LevelSelectController>, "Start")>]
    [<HarmonyPatch(typeof<SaveSlotController>, "checkScores")>]
    [<HarmonyPatch(typeof<SaverLoader>, "genBlankScores")>]
    static member PatchLength(instructions: CodeInstruction seq) : CodeInstruction seq =
        CodeMatcher(instructions)
            .MatchForward(false, [|
                CodeMatch(fun ins -> ins.LoadsField(tracktitles_f))
                CodeMatch(OpCodes.Ldlen)
            |]).Repeat(fun matcher ->
                matcher.RemoveInstructions(2)
                    .Insert(CodeInstruction.Call(typeof<TrackCountAccessor>, "trackCount"))
                    |> ignore
            ).InstructionEnumeration()

    [<HarmonyTranspiler>]
    [<HarmonyPatch(typeof<SaveSlotController>, "checkScores")>]
    [<HarmonyPatch(typeof<SaverLoader>, "genBlankScores")>]
    static member PatchAccess(instructions: CodeInstruction seq) : CodeInstruction seq =
        let matcher =
            CodeMatcher(instructions).MatchForward(false, [|
                CodeMatch(fun ins -> ins.LoadsField(tracktitles_f))
                CodeMatch(fun ins -> ins.IsLdloc())
                CodeMatch(OpCodes.Ldelem_Ref)
                CodeMatch(fun ins -> ins.LoadsConstant())
                CodeMatch(OpCodes.Ldelem_Ref)
            |])

        matcher.Repeat(fun matcher ->
            let lf_labels = matcher.Labels // get labels of LoadsField
            matcher.RemoveInstruction() // remove LoadsField
                .AddLabels(lf_labels) // re-apply labels to ldloc
                .Advance(1) // advance to ldelem_ref
                .SetInstruction(CodeInstruction.Call(typeof<TrackCountAccessor>, "trackrefByIndex", [| typeof<int> |]))
                .Advance(1) // advance to ldc
                .RemoveInstructions(2) // remove ldc and ldelem_ref
                |> ignore
        ).InstructionEnumeration()
