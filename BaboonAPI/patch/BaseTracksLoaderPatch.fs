namespace BaboonAPI.Patch

open System.Reflection.Emit
open BaboonAPI.Hooks.Tracks
open BaboonAPI.Internal
open BaboonAPI.Internal.BaseGame
open BaboonAPI.Internal.Customs
open BaboonAPI.Internal.Tootmaker
open BaboonAPI.Utility.Coroutines
open HarmonyLib
open UnityEngine
open UnityEngine.UI

type private BaseTracksLoaderAccessor =
    static let btn_reload_f = AccessTools.Field(typeof<HomeController>, "btn_reloadcustoms")
    static let collections_txt_f = AccessTools.Field(typeof<HomeController>, "txt_collections_list")

    static member reloadCollections () =
        let loader = GlobalVariables.track_collection_loader

        TrackAccessor.loadCollectionsAsync()
        |> run
        |> loader.StartCoroutine
        |> ignore

    static member onClickReload (controller: HomeController) =
        controller.StartCoroutine(coroutine {
            let! _ = TrackAccessor.loadAsync() // TODO error handling
            do! TrackAccessor.loadCollectionsAsync()

            controller.Invoke ("setCustomsPanelText", 0f)

            let btn_reload = unbox<GameObject> (btn_reload_f.GetValue controller)
            let collections_txt = unbox<Text> (collections_txt_f.GetValue controller)
            LeanTween.scaleY(collections_txt.gameObject, 1f, 0.15f)
                .setEaseOutBack()
                .setOnComplete(fun () -> btn_reload.SetActive true)
                |> ignore
        }) |> ignore

[<HarmonyPatch(typeof<TrackCollections>)>]
type LoaderPatch() =
    [<HarmonyPrefix>]
    [<HarmonyPatch("buildTrackCollections")>]
    static member PatchCollectionSetup (__instance: TrackCollections, ___string_localizer: StringLocalizer, ___collection_art_defaults: Sprite array) =
        let sprites = BaseGameCollectionSprites ___collection_art_defaults

        let basePath = $"{Application.streamingAssetsPath}/trackassets"
        let registry = BaseGameTrackRegistry (basePath, ___string_localizer, sprites)
        TrackRegistrationEvent.EVENT.Register registry
        TrackCollectionRegistrationEvent.EVENT.Register registry

        let tootmakerPath = GlobalVariables.localsettings.collections_path_tootmaker
        let registry = TootmakerTrackRegistry (tootmakerPath, ___string_localizer, sprites)
        TrackRegistrationEvent.EVENT.Register registry
        TrackCollectionRegistrationEvent.EVENT.Register registry
        CustomTrackLoaderEvent.EVENT.Register registry

        let customsPath = GlobalVariables.localsettings.collections_path_custom
        let registry = CustomCollectionsRegistry (customsPath, ___string_localizer, sprites)
        TrackRegistrationEvent.EVENT.Register registry
        TrackCollectionRegistrationEvent.EVENT.Register registry

        false

    [<HarmonyPrefix>]
    [<HarmonyPatch("rebuildCustomTrackCollections")>]
    static member PatchCollectionReload (__instance: TrackCollections) =
        false

    [<HarmonyPrefix>]
    [<HarmonyPatch("buildFavoritesCollection")>]
    static member PatchFavoritesReload (__instance: TrackCollections) =
        TrackAccessor.updateCollections()
        false


[<HarmonyPatch(typeof<SaverLoader>, "loadAllSaveHighScores")>]
type TrackLoadingPatch() =
    static member Prefix () =
        BaseTracksLoaderAccessor.reloadCollections()
        false

[<HarmonyPatch(typeof<HomeController>, "clickReloadCustoms")>]
type ReloadButtonPatch() =
    static let load_scores_m = AccessTools.Method(typeof<SaverLoader>, "loadAllSaveHighScores")

    static member Transpiler (instructions: CodeInstruction seq): CodeInstruction seq =
        let matcher = CodeMatcher(instructions)
        matcher
            .MatchForward(false, [|
                CodeMatch OpCodes.Ldc_I4_1
                CodeMatch OpCodes.Ldc_I4_0
                CodeMatch (fun ins -> ins.Calls load_scores_m)
            |])
            .ThrowIfInvalid("Could not find loadAllSaveHighScores call")
            .RemoveInstructions(3)
            .InsertAndAdvance([|
                CodeInstruction OpCodes.Ldarg_0
                CodeInstruction.Call(typeof<BaseTracksLoaderAccessor>, "onClickReload")
                CodeInstruction OpCodes.Ret
            |])
            .InstructionEnumeration()
