module BaboonAPI.Hooks.TrackReloader

open System
open BaboonAPI.Hooks.Tracks
open BaboonAPI.Internal
open BaboonAPI.Utility

/// <summary>Reload the list of tracks and collections asynchronously.</summary>
/// <param name="onProgress">Progress callback, will receive all progress events</param>
/// <returns>A YieldTask that must be started using StartCoroutine.</returns>
let reloadAll (onProgress: Progress -> unit) = Unity.task {
    match! TrackAccessor.loadAsyncWithProgress onProgress with
    | Ok () ->
        do! TrackAccessor.loadCollectionsAsyncWithProgress onProgress
        return Ok ()
    | Error e ->
        return Error e
}

/// <summary>Reload the list of tracks and collections asynchronously.</summary>
/// <param name="onProgress">Progress callback, will receive all progress events</param>
/// <remarks>C# Action variant.</remarks>
/// <returns>A YieldTask that must be started using StartCoroutine.</returns>
let ReloadAll (onProgress: Progress Action) = reloadAll (FuncConvert.FromAction onProgress)

/// <summary>Reload the list of tracks asynchronously, then update existing collections with the new tracks.</summary>
/// <remarks>The <paramref name="onProgress"/> callback will only be called up to FirstStageDone.</remarks>
/// <param name="onProgress">Progress callback, will received events up to FirstStageDone</param>
/// <returns>A YieldTask that must be started using StartCoroutine.</returns>
let reloadTracks (onProgress: Progress -> unit) = Unity.task {
    match! TrackAccessor.loadAsyncWithProgress onProgress with
    | Ok () ->
        TrackAccessor.updateCollections()
        return Ok ()
    | Error e ->
        return Error e
}

/// <summary>Reload the list of tracks asynchronously, then update existing collections with the new tracks.</summary>
/// <param name="onProgress">Progress callback, will received events up to FirstStageDone</param>
/// <remarks>C# Action variant.</remarks>
/// <returns>A YieldTask that must be started using StartCoroutine.</returns>
let ReloadTracks (onProgress: Progress Action) = reloadTracks (FuncConvert.FromAction onProgress)
