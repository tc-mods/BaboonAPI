namespace BaboonAPI.Hooks.Saves

open BepInEx
open Newtonsoft.Json.Linq

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
    abstract Load: data: 'a -> unit

    /// <summary>Used to convert the incoming JSON back into your type.</summary>
    /// <remarks>You should usually implement this function as follows:
    /// <code>member this.Convert o = o.ToObject()</code>
    /// </remarks>
    abstract Convert: o: JObject -> 'a

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
    abstract Attach: name: string -> target: ICustomSaveData<'a> -> unit

type private PluginSaverLoader(pluginGuid: string, attacher: SaverCapability -> unit) =
    member _.Save (pluginData: Map<string, obj>) =
        let mutable pluginData = pluginData

        attacher { new SaverCapability with
                     member _.Attach name sd =
                         pluginData <- pluginData.Add ($"{pluginGuid}/{name}", sd.Save()) }

        pluginData

    member _.Load (pluginData: Map<string, JObject>) =
        attacher { new SaverCapability with
                     member _.Attach name sd =
                         pluginData[$"{pluginGuid}/{name}"] |> sd.Convert |> sd.Load }

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
        pluginSavers
        |> Seq.fold (fun state saver -> saver.Save state) Map.empty
        |> Map.map (fun _ -> JObject.FromObject)

    let internal LoadAll (pluginData: Map<string, JObject>) =
        for saver in pluginSavers do
            saver.Load pluginData
