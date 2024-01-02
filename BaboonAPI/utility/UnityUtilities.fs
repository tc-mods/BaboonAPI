module BaboonAPI.Utility.Unity

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
