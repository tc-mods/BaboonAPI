namespace BaboonAPI.Hooks.Saves

open BepInEx

/// <summary>Implement if you want your class to be able to store persistent data.</summary>
/// <remarks>
/// The save &amp; load methods should return and accept a class marked with
/// <see cref="T:System.SerializableAttribute">Serializable</see>.
/// </remarks>
type ICustomSaveData<'a> =
    /// Called to save this object to disk.
    /// The returned object should be serializable.
    abstract Save: unit -> 'a

    /// Called when saved data is loaded from disk.
    /// You can use this to restore your class's state.
    abstract Load: 'a -> unit

/// Represents something with the capability to save or load objects.
type SaverCapability =
    /// <summary>Call to attach an object to the current save.</summary>
    /// <remarks>
    /// You should provide a unique name for each attachment.
    ///
    /// The object's <see cref="M:BaboonAPI.Hooks.Saves.ICustomSaveData`1.Save">Save</see> and
    /// <see cref="M:BaboonAPI.Hooks.Saves.ICustomSaveData`1.Load(`0)">Load</see>
    /// functions will be called when the game saves or loads.
    /// </remarks>
    /// <param name="name">Name to save this object as.</param>
    /// <param name="target">Serializable object</param>
    abstract Attach: name: string -> target: ICustomSaveData<obj> -> unit

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

/// <summary>Persistent data API</summary>
/// <remarks>
/// Used for persisting your mod data to disk.
/// </remarks>
module CustomSaveRegistry =
    let mutable private pluginSavers: PluginSaverLoader list = []

    /// <summary>Register the saveable objects for your plugin.</summary>
    /// <remarks>
    /// Note that the <paramref name="attach" /> callback will be called multiple times,
    /// and thus should not have any side effects.
    /// </remarks>
    /// <param name="info">Plugin info instance, used to namespace save entries.</param>
    /// <param name="attach">Attachment callback, call
    /// <see cref="M:BaboonAPI.Hooks.Saves.SaverCapability.Attach(System.String,BaboonAPI.Hooks.Saves.ICustomSaveData{System.Object})">
    /// Attach</see> to attach saveable objects.</param>
    let Register (info: PluginInfo) (attach: SaverCapability -> unit) =
        pluginSavers <- PluginSaverLoader (info.Metadata.GUID, attach) :: pluginSavers
        ()

    let internal SaveAll () =
        pluginSavers |> Seq.fold (fun state saver -> saver.Save state) Map.empty

    let internal LoadAll (pluginData: Map<string, obj>) =
        for saver in pluginSavers do
            saver.Load pluginData
