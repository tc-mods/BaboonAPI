﻿namespace BaboonAPI.Patch

open BaboonAPI.Hooks.Tracks
open BaboonAPI.Internal
open BaboonAPI.Internal.BaseGame
open HarmonyLib
open UnityEngine

[<HarmonyPatch(typeof<TrackCollections>)>]
type LoaderPatch() =
    [<HarmonyPrefix>]
    [<HarmonyPatch("buildTrackCollections")>]
    static member PatchCollectionSetup (__instance: TrackCollections, ___string_localizer: StringLocalizer, ___collection_art_defaults: Sprite array) =
        let path = $"{Application.streamingAssetsPath}/trackassets"
        let sprites = BaseGameCollectionSprites ___collection_art_defaults
        let registry = BaseGameTrackRegistry (path, ___string_localizer, sprites)
        TrackRegistrationEvent.EVENT.Register registry
        TrackCollectionRegistrationEvent.EVENT.Register registry

        false

    [<HarmonyPrefix>]
    [<HarmonyPatch("rebuildCustomTrackCollections")>]
    static member PatchCollectionReload (__instance: TrackCollections) =
        TrackAccessor.loadCollectionsAsync()
        |> __instance.StartCoroutine
        |> ignore

        false


[<HarmonyPatch(typeof<SaverLoader>, "loadAllSaveHighScores")>]
type TrackLoadingPatch() =
    static member Prefix () =
        let loader = GlobalVariables.track_collection_loader

        TrackAccessor.loadCollectionsAsync()
        |> loader.StartCoroutine
        |> ignore

        false
