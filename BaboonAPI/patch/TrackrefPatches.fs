namespace BaboonAPI.Patch

open System.Collections.Generic
open System.Reflection.Emit
open BaboonAPI.Internal
open BepInEx.Logging
open HarmonyLib

type private TrackrefAccessor() =
    static let logger = Logger.CreateLogSource "BaboonAPI.TrackrefAccessor"

    static let makeSingleTrackData (rt: TrackAccessor.RegisteredTrack) =
        let track = rt.track
        let data = SingleTrackData()
        data.trackname_long <- track.trackname_long
        data.trackname_short <- track.trackname_short
        data.trackindex <- rt.trackIndex
        data.artist <- track.artist
        data.year <- track.year
        data.desc <- track.desc
        data.difficulty <- track.difficulty
        data.genre <- track.genre
        data.length <- track.length
        data.tempo <- track.tempo
        data.trackref <- track.trackref
        data

    static member finalLevelIndex () = TrackAccessor.fetchTrackIndex "einefinal"

    static member trackrefForIndex i = (TrackAccessor.fetchTrackByIndex i).trackref
    static member trackTitleForIndex i = (TrackAccessor.fetchTrackByIndex i).trackname_long
    static member trackDifficultyForIndex i = (TrackAccessor.fetchTrackByIndex i).difficulty
    static member trackLengthForIndex i = (TrackAccessor.fetchTrackByIndex i).length

    static member fetchChosenTrack trackref =
        TrackAccessor.fetchRegisteredTrack trackref |> makeSingleTrackData

    static member doLevelSelectStart (instance: LevelSelectController, alltrackslist: List<SingleTrackData>) =
        try
            TrackAccessor.allTracks()
            |> Seq.filter (fun s -> s.track.IsVisible())
            |> Seq.map makeSingleTrackData
            |> alltrackslist.AddRange
        with
        | TrackAccessor.DuplicateTrackrefException trackref ->
            // TODO: Show an error popup in game? The game doesn't have anything for this...
            logger.LogFatal $"Duplicate trackref {trackref}, songs not loading!"

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
        let matcher = CodeMatcher(instructions).MatchForward(true, [|
            CodeMatch(fun ins -> ins.LoadsConstant())
            CodeMatch(fun ins -> ins.IsStloc())
        |])

        let start = matcher.Pos

        matcher
            .MatchForward(true, [|
                CodeMatch(fun ins -> ins.LoadsField(tracktitles_f))
                CodeMatch OpCodes.Ldlen
            |])
            .RemoveInstructionsInRange(start, matcher.Pos + 2) // Remove the for loop
            .Start()
            .Advance(start) // Go to where the for loop used to be
            .InsertAndAdvance([|
                CodeInstruction OpCodes.Ldarg_0
                CodeInstruction OpCodes.Ldarg_0
                CodeInstruction.LoadField(typeof<LevelSelectController>, "alltrackslist")
                CodeInstruction.Call(typeof<TrackrefAccessor>, "doLevelSelectStart",
                               [| typeof<LevelSelectController>; typeof<List<SingleTrackData>> |])
            |])
            .InstructionEnumeration()
