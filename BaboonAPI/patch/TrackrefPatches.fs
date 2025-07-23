namespace BaboonAPI.Patch

open BaboonAPI.Hooks.Tracks
open BaboonAPI.Internal
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
    static member fetchChosenTrack trackref =
        (TrackAccessor.fetchRegisteredTrack trackref).asTrackData

    static member doLevelSelectStart (instance: LevelSelectController, alltrackslist: ResizeArray<SingleTrackData>) =
        // instance.gameObject.AddComponent<LevelSelectReloadBehaviour>().Init(instance, alltrackslist)
        ()

[<HarmonyPatch(typeof<SaverLoader>, "loadTrackData")>]
type TrackLoaderPatch() =
    // Patches the track lookup function to use our registry :)
    static member Prefix (trackref: string) =
        GlobalVariables.chosen_track <- trackref
        GlobalVariables.chosen_track_data <- TrackrefAccessor.fetchChosenTrack trackref
        false

[<HarmonyPatch(typeof<LevelSelectController>)>]
type LevelSelectPatch() =
    [<HarmonyPrefix>]
    [<HarmonyPatch("Start")>]
    static member PatchStart (__instance: LevelSelectController, ___alltrackslist: ResizeArray<SingleTrackData>) =
        TrackrefAccessor.doLevelSelectStart(__instance, ___alltrackslist)
        true

    [<HarmonyPostfix>]
    [<HarmonyPatch("clickPlay")>]
    static member PatchPlay () =
        // We always want the non custom handling because we use a unified loading path
        GlobalVariables.playing_custom_track <- false
        ()

    [<HarmonyPrefix>]
    [<HarmonyPatch("checkForSongsToHide")>]
    static member PatchSongsVisible (__instance: LevelSelectController, ___alltrackslist: ResizeArray<SingleTrackData>) =
        let isStreamerMode trackref =
            if GlobalVariables.localsettings.streamer_mode then
                __instance.streaming_unfriendly_songs
                |> Array.contains trackref
            else false

        ___alltrackslist.RemoveAll(fun track ->
            let tt = TrackAccessor.fetchTrack track.trackref
            not (tt.IsVisible()) || isStreamerMode track.trackref
        ) |> ignore

        false
