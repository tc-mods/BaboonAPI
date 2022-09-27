namespace BaboonAPI.Patch

open System.Reflection.Emit
open BaboonAPI.Internal
open HarmonyLib

type internal TrackrefAccessor() =
    static let finalLevelIndex () =
        (TrackAccessor.fetchTrack "einefinal").trackindex

    static let trackrefForIndex =
        TrackAccessor.fetchTrackByIndex
        >> (fun t -> t.trackref)

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
        let iter = instructions.GetEnumerator()

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

        iter.Dispose()
    }

[<HarmonyPatch>]
type PointScenePatches() =
    static let trackrefs_f =
        AccessTools.Field(typeof<GlobalVariables>, nameof (GlobalVariables.data_trackrefs))

    static let throwIfFalse (b: bool) =
        if b = false then
            invalidArg "instructions" "Expected more elements in sequence"

    /// Patch various GlobalVariable lookups of trackrefs to use our track registry instead
    [<HarmonyTranspiler>]
    [<HarmonyPatch(typeof<PointSceneController>, "Start")>]
    [<HarmonyPatch(typeof<PointSceneController>, "updateSave")>]
    static member Transpiler(instructions: CodeInstruction seq) : CodeInstruction seq = seq {
        let iter = instructions.GetEnumerator()

        while iter.MoveNext() do
            let ins = iter.Current

            if ins.LoadsField(trackrefs_f) then
                iter.MoveNext() |> throwIfFalse // ldsfld int32 GlobalVariables::chosen_track_index
                yield iter.Current
                iter.MoveNext() |> throwIfFalse // ldelem.ref

                if (iter.Current.opcode = OpCodes.Ldelem_Ref) then
                    yield CodeInstruction.Call(typeof<TrackrefAccessor>, "trackrefForIndex", [| typeof<int> |])
            else
                yield ins

        iter.Dispose()
    }
