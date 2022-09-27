namespace BaboonAPI.Patch

open System.Reflection.Emit
open BaboonAPI.Internal
open HarmonyLib

type internal TrackCountAccessor() =
    static let trackCount = TrackAccessor.trackCount

    static let trackrefByIndex = TrackAccessor.fetchTrackByIndex >> (fun t -> t.trackref)

[<HarmonyPatch>]
type TrackCountPatches() =
    static let trackrefs_f =
        AccessTools.Field(typeof<GlobalVariables>, nameof GlobalVariables.data_trackrefs)

    /// Patches anywhere that reads the trackref length and replaces it with the "true" track count
    [<HarmonyTranspiler>]
    [<HarmonyPatch(typeof<LevelSelectController>, "Start")>]
    [<HarmonyPatch(typeof<SaveSlotController>, "checkScores")>]
    [<HarmonyPatch(typeof<SaverLoader>, "genBlankScores")>]
    static member PatchLength(instructions: CodeInstruction seq) : CodeInstruction seq = seq {
        yield instructions |> Seq.head // first instruction is only passed as "prev" so just yield it here

        for prev, cur in instructions |> Seq.pairwise do
            if cur.LoadsField(trackrefs_f) then
                () // hold to see if it's ldlen
            elif prev.LoadsField(trackrefs_f) then
                if cur.opcode = OpCodes.Ldlen then
                    yield CodeInstruction.Call(typeof<TrackCountAccessor>, "trackCount")
                else
                    yield prev // wasn't ldlen, let the ldsfld pass
            else
                yield cur
    }

    [<HarmonyTranspiler>]
    [<HarmonyPatch(typeof<SaveSlotController>, "checkScores")>]
    [<HarmonyPatch(typeof<SaverLoader>, "genBlankScores")>]
    static member PatchAccess(instructions: CodeInstruction seq) : CodeInstruction seq = seq {
        yield instructions |> Seq.head // first instruction is only passed as "prev" so just yield it here

        for prev, cur in instructions |> Seq.pairwise do
            if cur.LoadsField(trackrefs_f) then
                () // skip
            elif prev.opcode = OpCodes.Ldloc_3
                 && cur.opcode = OpCodes.Ldelem_Ref then
                yield CodeInstruction.Call(typeof<TrackCountAccessor>, "trackrefByIndex", [| typeof<int> |])
            else
                yield cur
    }
