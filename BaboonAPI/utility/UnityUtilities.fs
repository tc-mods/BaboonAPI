﻿module BaboonAPI.Utility.Unity

open System
open BaboonAPI.Utility.Coroutines
open UnityEngine
open UnityEngine.Networking

/// Asynchronously open an asset bundle from disk
let public openAssetBundleFromFile =
    AssetBundle.LoadFromFileAsync >> awaitAssetBundle

/// Asynchronously open an asset bundle from the given stream
let public openAssetBundleFromStream<'a> =
    AssetBundle.LoadFromStreamAsync >> awaitAssetBundle

/// Asynchronously load an asset from a bundle
let public loadAsset (name: string) (bundle: AssetBundle) =
    bundle.LoadAssetAsync name
    |> awaitAsyncOperation (fun op -> op.asset)

let private mapResult (binder: UnityWebRequestAsyncOperation -> 'a) (op: UnityWebRequestAsyncOperation) =
    try
        if op.webRequest.isHttpError || op.webRequest.isNetworkError then
            Error op.webRequest.error
        else
            Ok (binder op)
    finally
        op.webRequest.Dispose()

/// Load an audio clip from file asynchronously
let public loadAudioClip (path: string, audioType: AudioType) =
    let uri = UriBuilder(Uri.UriSchemeFile, String.Empty, Path = path).Uri
    let www = UnityWebRequestMultimedia.GetAudioClip (uri, audioType)
    match www.downloadHandler with
    | :? DownloadHandlerAudioClip as handler ->
        handler.streamAudio <- true
        handler.compressed <- false
    | _ -> ()

    awaitAsyncOperation (mapResult (fun op -> DownloadHandlerAudioClip.GetContent op.webRequest)) (www.SendWebRequest ())

/// Load a texture from file asynchronously
let public loadTexture (path: string) =
    let uri = UriBuilder(Uri.UriSchemeFile, String.Empty, Path = path).Uri
    let www = UnityWebRequestTexture.GetTexture uri

    awaitAsyncOperation (mapResult (fun op -> DownloadHandlerTexture.GetContent op.webRequest)) (www.SendWebRequest ())

/// Send a web request and map the result
let public makeRequest (binder: UnityWebRequestAsyncOperation -> 'a) (www: UnityWebRequest) =
    awaitAsyncOperation (mapResult binder) (www.SendWebRequest ())

type UnityTaskBuilder() =
    member _.Yield (yi: YieldInstruction) =
        YieldTask(Seq.singleton yi, id)

    member _.Yield (yi: CustomYieldInstruction) =
        let coro = seq {
            while yi.MoveNext() do
                yield yi.Current :?> YieldInstruction
        }

        YieldTask(coro, id)

    member _.Bind (src: YieldTask<'a>, binder: 'a -> YieldTask<'b>) =
        bind binder src

    member _.For (expr: 'a seq, binder: 'a -> YieldTask<'b>) =
        let mutable result = None
        let coro = seq {
            for item in expr do
                let src = binder(item)
                yield! src.Coroutine
                result <- Some src.Result
        }

        YieldTask(coro, fun () -> Option.get result)

    member this.TryWith (delayed: unit -> YieldTask<'a>, binder: exn -> YieldTask<'a>) =
        let mutable result = None
        let coro = seq {
            try
                let src = delayed()
                yield! src.Coroutine
                result <- Some src.Result
            with
            | err ->
                let src = binder err
                yield! src.Coroutine
                result <- Some src.Result
        }

        YieldTask(coro, fun () -> Option.get result)

    member _.Return (item: 'a) =
        YieldTask(Seq.empty, fun () -> item)

    member _.ReturnFrom (task: YieldTask<'a>) = task

    member _.Combine (a: YieldTask<'a>, b: unit -> YieldTask<'b>) =
        let mutable result = None
        let coro = seq {
            yield! a.Coroutine

            let src = b()
            yield! src.Coroutine
            result <- Some src.Result
        }

        YieldTask(coro, fun () -> Option.get result)

    member _.Zero () =
        YieldTask(Seq.empty, fun () -> ())

    member _.Delay (binder: unit -> YieldTask<'a>) = binder

    member _.Run (delayed: unit -> YieldTask<'a>) = delayed()

let public task = UnityTaskBuilder()
