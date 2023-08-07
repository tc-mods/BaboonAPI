namespace BaboonAPI.Patch

open System
open System.Reflection.Emit
open BaboonAPI.Hooks.Tracks
open BaboonAPI.Internal
open BepInEx.Logging
open HarmonyLib
open UnityEngine

type private LevelSelectContext =
    { controller: LevelSelectController
      allTracksList: SingleTrackData ResizeArray }

type private LevelSelectReloadBehaviour() =
    inherit MonoBehaviour()

    static let songindex_f = AccessTools.Field(typeof<LevelSelectController>, "songindex")
    static let populate_names_m = AccessTools.Method(typeof<LevelSelectController>, "populateSongNames")

    // Unity sucks etc etc
    let mutable context = None

    member _.Init (controller: LevelSelectController, alltrackslist: ResizeArray<SingleTrackData>) =
        context <- Some { controller = controller; allTracksList = alltrackslist }

    member this.Start () =
        TracksLoadedEvent.EVENT.Register this
        ()

    member this.OnDestroy () =
        TracksLoadedEvent.EVENT.Unregister this
        ()

    interface TracksLoadedEvent.Listener with
        member this.OnTracksLoaded _ =
            match context with
            | Some ctx ->
                ctx.allTracksList.Clear()

                TrackAccessor.allTracks()
                |> Seq.filter (fun s -> s.track.IsVisible())
                |> Seq.map (fun s -> s.asTrackData)
                |> ctx.allTracksList.AddRange

                songindex_f.SetValue (ctx.controller, 0)
                populate_names_m.Invoke (ctx.controller, [| false |]) |> ignore
                ()
            | _ -> ()

type private TrackrefAccessor() =
    static let logger = Logger.CreateLogSource "BaboonAPI.TrackrefAccessor"

    static let makeSongGraph (rt: TrackAccessor.RegisteredTrack) =
        let generate _ =
            Mathf.Clamp(rt.track.difficulty * 10 + Random.Range (-25, 5), 10, 104)

        let graph =
            match rt.track with
            | :? Graphable as graphable ->
                graphable.CreateGraph()
            | _ -> None

        match graph with
        | Some g ->
            g.asArray
        | None ->
            Array.init 5 generate

    static member finalLevelIndex () = TrackAccessor.fetchTrackIndex "einefinal"

    static member trackrefForIndex i = (TrackAccessor.fetchTrackByIndex i).trackref
    static member trackTitleForIndex i = (TrackAccessor.fetchTrackByIndex i).trackname_long
    static member trackDifficultyForIndex i = (TrackAccessor.fetchTrackByIndex i).difficulty
    static member trackLengthForIndex i = (TrackAccessor.fetchTrackByIndex i).length
    static member trackCount () = TrackAccessor.trackCount()

    static member fetchChosenTrack trackref =
        (TrackAccessor.fetchRegisteredTrack trackref).asTrackData

    static member doLevelSelectStart (instance: LevelSelectController, alltrackslist: ResizeArray<SingleTrackData>) =
        instance.gameObject.AddComponent<LevelSelectReloadBehaviour>().Init(instance, alltrackslist)

        TrackAccessor.allTracks()
        |> Seq.filter (fun s -> s.track.IsVisible())
        |> Seq.map (fun s -> s.asTrackData)
        |> alltrackslist.AddRange

    static member populateSongGraphs () =
        TrackAccessor.allTracks()
        |> Seq.map makeSongGraph
        |> Array.ofSeq

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
                .MatchForward(true, [|
                    CodeMatch(fun ins -> ins.LoadsField(tracktitles_f))
                    CodeMatch OpCodes.Ldlen
                    CodeMatch ()
                    CodeMatch (fun ins -> ins.Branches () |> fst)
                |])
                .ThrowIfInvalid("Could not find end of injection point in LevelSelectController#Start")

        let endPos = matcher.Pos
        let branch =
            match matcher.Instruction.Branches() with
            | true, b -> b.Value
            | false, _ -> raise (InvalidOperationException "Could not find branching instruction of for loop in LevelSelectController#Start")

        let startPos =
            matcher
                .SearchBack(fun ins -> ins.labels.Contains branch)
                .ThrowIfInvalid("Could not find start of injection point in LevelSelectController#Start")
                .Pos - 3

        matcher
            .RemoveInstructionsInRange(startPos, endPos) // Remove the for loop
            .Start()
            .Advance(startPos) // Go to where the for loop used to be
            .InsertAndAdvance([|
                CodeInstruction OpCodes.Ldarg_0
                CodeInstruction OpCodes.Ldarg_0
                CodeInstruction.LoadField(typeof<LevelSelectController>, "alltrackslist")
                CodeInstruction.Call(typeof<TrackrefAccessor>, "doLevelSelectStart",
                               [| typeof<LevelSelectController>; typeof<ResizeArray<SingleTrackData>> |])

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
