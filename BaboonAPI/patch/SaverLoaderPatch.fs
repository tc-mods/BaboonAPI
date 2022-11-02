namespace BaboonAPI.Patch

open System
open System.IO
open BaboonAPI.Hooks.Saves
open BepInEx.Logging
open HarmonyLib
open Newtonsoft.Json
open UnityEngine

module private CustomSaveController =
    let CustomSaveVersion = 1

    exception IncompatibleVersion of int * int

    [<Serializable>]
    [<CLIMutable>]
    type BaboonSaveData =
        { [<JsonProperty("Version")>] Version: int
          [<JsonProperty("PluginData")>] PluginData: Map<string, obj> }

    let private serializer = JsonSerializer.Create()

    let dataPath (index: int) = $"{Application.persistentDataPath}/baboonapi_save_{index}.json"

    let SavePluginData (index: int) =
        let data =
            { Version = CustomSaveVersion
              PluginData = CustomSaveRegistry.SaveAll() }

        use fd = File.Open(dataPath index, FileMode.Create)
        use stream = new StreamWriter(fd)
        use writer = new JsonTextWriter(stream)

        serializer.Serialize (writer, data)

    let LoadPluginData (index: int) =
        use fd = File.Open(dataPath index, FileMode.Open)
        use stream = new StreamReader(fd)
        use reader = new JsonTextReader(stream)

        let data = serializer.Deserialize<BaboonSaveData> reader
        if data.Version = CustomSaveVersion then
            CustomSaveRegistry.LoadAll data.PluginData
        else
            raise (IncompatibleVersion (CustomSaveVersion, data.Version))

[<HarmonyPatch(typeof<SaverLoader>)>]
type SaverLoaderPatch() =
    static let logger = Logger.CreateLogSource "BaboonAPI.SaverLoaderPatch"

    [<HarmonyPostfix>]
    [<HarmonyPatch("loadSavedGame")>]
    static member LoadPostfix() =
        try
            CustomSaveController.LoadPluginData(SaverLoader.global_saveindex)
        with
        | :? FileNotFoundException ->
            logger.LogInfo "No custom save file yet, continuing"
        | CustomSaveController.IncompatibleVersion (expected, actual) ->
            logger.LogError $"The plugin save data is version {actual}, but this BaboonAPI version expects {expected}."

    [<HarmonyPostfix>]
    [<HarmonyPatch("updateSavedGame")>]
    static member SavePostfix() =
        CustomSaveController.SavePluginData(SaverLoader.global_saveindex)
