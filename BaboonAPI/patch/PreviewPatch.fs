namespace BaboonAPI.Patch

open System.Reflection
open BaboonAPI.Hooks.Tracks
open BaboonAPI.Internal
open BaboonAPI.Utility.Coroutines
open BepInEx.Logging
open HarmonyLib
open UnityEngine

[<HarmonyPatch>]
type PreviewPatch() =
    static let logger = Logger.CreateLogSource "BaboonAPI.PreviewPatch"

    static let setClipAndFade (clip: TrackAudio) (player: LevelSelectClipPlayer) =
        let pt = player.GetType()
        pt.GetField("clip_volume", BindingFlags.NonPublic ||| BindingFlags.Instance).SetValue (player, clip.Volume)

        let clipPlayer: AudioSource = unbox (pt.GetField("clipPlayer", BindingFlags.NonPublic ||| BindingFlags.Instance).GetValue player)
        clipPlayer.clip <- clip.Clip
        clipPlayer.volume <- 0f
        clipPlayer.Play()

        pt.GetMethod("startCrossFade", BindingFlags.NonPublic ||| BindingFlags.Instance).Invoke (player, [| 1f |])
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
