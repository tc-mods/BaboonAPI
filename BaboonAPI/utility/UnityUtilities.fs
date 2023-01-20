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
    use www = UnityWebRequestMultimedia.GetAudioClip (path, audioType)

    let mapResult (op: UnityWebRequestAsyncOperation) =
        if op.webRequest.isHttpError || op.webRequest.isNetworkError then
            Error op.webRequest.error
        else
            Ok (DownloadHandlerAudioClip.GetContent op.webRequest)

    awaitAsyncOperation mapResult (www.SendWebRequest ())
