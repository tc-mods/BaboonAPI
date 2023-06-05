module BaboonAPI.Utility.Unity

open BaboonAPI.Utility.Coroutines
open UnityEngine
open UnityEngine.Networking

let public openAssetBundleFromFile =
    AssetBundle.LoadFromFileAsync >> awaitAssetBundle

let public openAssetBundleFromStream<'a> =
    AssetBundle.LoadFromStreamAsync >> awaitAssetBundle

let public loadAsset (name: string) (bundle: AssetBundle) =
    bundle.LoadAssetAsync name
    |> awaitAsyncOperation (fun op -> op.asset)

let public loadAudioClip (path: string, audioType: AudioType) =
    let www = UnityWebRequestMultimedia.GetAudioClip (path, audioType)
    match www.downloadHandler with
    | :? DownloadHandlerAudioClip as handler ->
        handler.streamAudio <- true
        handler.compressed <- false
    | _ -> ()

    let mapResult (op: UnityWebRequestAsyncOperation) =
        try
            if op.webRequest.isHttpError || op.webRequest.isNetworkError then
                Error op.webRequest.error
            else
                Ok (DownloadHandlerAudioClip.GetContent op.webRequest)
        finally
            op.webRequest.Dispose()

    awaitAsyncOperation mapResult (www.SendWebRequest ())
