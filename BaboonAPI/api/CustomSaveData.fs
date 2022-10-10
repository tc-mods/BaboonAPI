namespace BaboonAPI.Hooks.Saves

open BepInEx

type ICustomSaveData =
    abstract Save: unit -> obj
    abstract Load: obj -> unit

type SaverCapability =
    abstract Attach: string -> ICustomSaveData -> unit

type private PluginSaverLoader(pluginGuid: string, attacher: SaverCapability -> unit) =
    member _.Save (pluginData: Map<string, obj>) =
        let mutable pluginData = pluginData

        attacher { new SaverCapability with
                     member _.Attach name sd =
                         pluginData <- pluginData.Add ($"{pluginGuid}/{name}", sd.Save()) }

        pluginData

    member _.Load (pluginData: Map<string, obj>) =
        attacher { new SaverCapability with
                     member _.Attach name sd =
                         pluginData[$"{pluginGuid}/{name}"] |> sd.Load }

module CustomSaveRegistry =
    let mutable private pluginSavers: PluginSaverLoader list = []

    let Register (info: PluginInfo) (attach: SaverCapability -> unit) =
        pluginSavers <- PluginSaverLoader (info.Metadata.GUID, attach) :: pluginSavers
        ()

    let internal SaveAll () =
        pluginSavers |> Seq.fold (fun state saver -> saver.Save state) Map.empty

    let internal LoadAll (pluginData: Map<string, obj>) =
        for saver in pluginSavers do
            saver.Load(pluginData)
