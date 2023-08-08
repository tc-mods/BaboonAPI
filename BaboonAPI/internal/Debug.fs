module internal BaboonAPI.Internal.Debug

open System
open System.IO
open BepInEx
open BepInEx.Logging
open Newtonsoft.Json
open Steamworks
open UnityEngine

let checkSteam () =
    SteamManager.Initialized && SteamUser.GetSteamID().IsValid()

let buildLauncherInfo () =
    let isInGameDir = Path.Combine(Paths.GameRootPath, "BepInEx") = Paths.BepInExRootPath

    {| BepInPath = Paths.BepInExRootPath
       AreModsInGameDir = isInGameDir |}

let buildDebugPayload () =
    {| GameVersion = Application.version
       UnityVersion = Application.unityVersion
       UnityPlatform = Enum.GetName (typeof<RuntimePlatform>, Application.platform)
       Platform = Environment.OSVersion.ToString()
       SteamValid = checkSteam()
       Launcher = buildLauncherInfo()
       Timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds() |}

let printDebug (logger: ManualLogSource) =
    let payload = buildDebugPayload()
    let serialized = JsonConvert.SerializeObject payload

    logger.LogInfo $"ENV_DATA:{serialized}"
    ()
