namespace BaboonAPI.Patch

open BaboonAPI.Hooks.Tracks
open BaboonAPI.Internal
open BaboonAPI.Utility.Coroutines
open BepInEx.Logging
open HarmonyLib
open UnityEngine

[<HarmonyPatch>]
type PreviewPatch() =
    static let logger = Logger.CreateLogSource "BaboonAPI.PreviewPatch"

    static let clip_volume_f = AccessTools.Field(typeof<LevelSelectClipPlayer>, "clip_volume")
    static let clip_player_f = AccessTools.Field(typeof<LevelSelectClipPlayer>, "clipPlayer")
    static let start_fade_m = AccessTools.Method(typeof<LevelSelectClipPlayer>, "startCrossFade")

    static let setClipAndFade (clip: TrackAudio) (player: LevelSelectClipPlayer) =
        clip_volume_f.SetValue (player, clip.Volume)

        let clipPlayer: AudioSource = unbox (clip_player_f.GetValue player)
        clipPlayer.clip <- clip.Clip
        clipPlayer.volume <- 0f
        clipPlayer.Play()

        start_fade_m.Invoke (player, [| 1f |])
        |> ignore

    static let doClipNotFound (player: LevelSelectClipPlayer) =
        player.Invoke("doDefaultClipNotFoundEvent", 0f)

    [<HarmonyPrefix>]
    [<HarmonyPatch(typeof<LevelSelectClipPlayer>, "beginClipSearch")>]
    static member ClipSearchPrefix(__instance: LevelSelectClipPlayer, ___current_trackref: string inref) =
        let track =
            TrackAccessor.tryFetchRegisteredTrack ___current_trackref
            |> Option.map (fun rt -> rt.track)

        match track with
        | Some (:? Previewable as preview) ->
            coroutine {
                let! clip = preview.LoadClip()

                match clip with
                | Ok audio ->
                    setClipAndFade audio __instance
                | Error msg ->
                    logger.LogError $"Failed to load song preview clip: {msg}"
                    doClipNotFound __instance
            } |> __instance.StartCoroutine |> ignore
        | _ ->
            doClipNotFound __instance

        false
