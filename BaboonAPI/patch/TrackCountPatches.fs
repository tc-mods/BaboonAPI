namespace BaboonAPI.Patch

open System.Reflection.Emit
open BaboonAPI.Internal
open HarmonyLib

type private TrackCountAccessor() =
    static member trackCount () = TrackAccessor.trackCount()

    static member trackrefByIndex i = TrackAccessor.fetchTrackByIndex i |> (fun t -> t.trackref)

[<HarmonyPatch>]
type TrackCountPatches() =
    static let trackrefs_f =
        AccessTools.Field(typeof<GlobalVariables>, nameof GlobalVariables.data_trackrefs)

    /// Patches anywhere that reads the trackref length and replaces it with the "true" track count
    [<HarmonyTranspiler>]
    [<HarmonyPatch(typeof<LevelSelectController>, "Start")>]
    [<HarmonyPatch(typeof<SaveSlotController>, "checkScores")>]
    [<HarmonyPatch(typeof<SaverLoader>, "genBlankScores")>]
    static member PatchLength(instructions: CodeInstruction seq) : CodeInstruction seq =
        CodeMatcher(instructions)
            .MatchForward(false, [|
                CodeMatch(fun ins -> ins.LoadsField(trackrefs_f))
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
                CodeMatch(fun ins -> ins.LoadsField(trackrefs_f))
                CodeMatch(fun ins -> ins.IsLdloc())
                CodeMatch(OpCodes.Ldelem_Ref)
            |])

        matcher.Repeat(fun matcher ->
            let lf_labels = matcher.Labels // get labels of LoadsField
            matcher.RemoveInstruction() // remove LoadsField
                .AddLabels(lf_labels) // re-apply labels to ldloc
                .Advance(1) // advance to ldelem_ref
                .SetInstruction(CodeInstruction.Call(typeof<TrackCountAccessor>, "trackrefByIndex", [| typeof<int> |]))
                |> ignore
        ).InstructionEnumeration()
