namespace BaboonAPI.Patch

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

    static member trackrefForIndex i =
        TrackAccessor.fetchTrackByIndex i
        |> (fun t -> t.trackref)

    static member doLevelSelectStart (instance: LevelSelectController) =
        instance.sortdrop.SetActive false
        TrackAccessor.allTracks()
        |> Seq.choose makeSingleTrackData
        |> instance.alltrackslist.AddRange

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
type PointScenePatches() =
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
type LevelControllerPatch() =
    static let tracktitles_f = AccessTools.Field(typeof<GlobalVariables>, nameof GlobalVariables.data_tracktitles)

    [<HarmonyTranspiler>]
    [<HarmonyPatch(typeof<LevelSelectController>, "Start")>]
    static member Transpiler(instructions: CodeInstruction seq): CodeInstruction seq = seq {
        use e = instructions.GetEnumerator()
        let mutable skipping = true

        while skipping && e.MoveNext() do
            if e.Current.LoadsField(tracktitles_f) then
                e.MoveNext() |> ignore // check next instruction
                if e.Current.opcode = OpCodes.Ldlen then
                    skipping <- false // found our data_tracktitles.Length call, break loop

        e.MoveNext() |> ignore // conv.i4
        e.MoveNext() |> ignore // blt

        // Put in our call instead
        yield CodeInstruction (OpCodes.Ldarg_0)
        yield CodeInstruction.Call(typeof<TrackrefAccessor>, "doLevelSelectStart", [| typeof<LevelSelectController> |])

        // Yield everything else
        while e.MoveNext() do
            yield e.Current
    }
