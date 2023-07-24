module internal BaboonAPI.Internal.EntryPointScanner

open System
open System.Reflection
open BaboonAPI.Hooks.Entrypoints
open BepInEx
open BepInEx.Bootstrap
open BepInEx.Logging

let logger = Logger.CreateLogSource "BaboonAPI.EntryPointScanner"

let getCustomAttribute<'t when 't :> Attribute> (target: Type): 't option =
    match box (target.GetCustomAttribute<'t>()) with
    | null -> None
    | x -> Some (unbox x)

type PossibleConstructors =
    | NoneFound
    | ZeroArg of ConstructorInfo
    | PluginInfoArg of ConstructorInfo
    | PluginArg of ConstructorInfo

let findConstructor (candidate: Type, pluginType: Type): PossibleConstructors =
    let pluginArg =
        Option.ofObj (candidate.GetConstructor [| pluginType |])
        |> Option.map PluginArg
    
    let pluginInfoArg = (fun () ->
        Option.ofObj (candidate.GetConstructor [| typeof<PluginInfo> |])
        |> Option.map PluginInfoArg)

    let zeroArg = (fun () ->
        Option.ofObj (candidate.GetConstructor [||])
        |> Option.map ZeroArg)

    pluginArg
    |> Option.orElseWith pluginInfoArg
    |> Option.orElseWith zeroArg
    |> Option.defaultValue NoneFound

/// Scan a plugin for hook subclasses
let scan<'t> (plugin: PluginInfo): 't EntryPointContainer list =
    let target = typeof<'t>
    let pluginType = plugin.Instance.GetType()
    let assembly = pluginType.Assembly

    let candidates =
        assembly.GetTypes()
        |> Seq.filter target.IsAssignableFrom
        |> Seq.filter (getCustomAttribute<BaboonEntryPointAttribute> >> Option.isSome)

    let construct (candidate: Type) =
        let constructor = findConstructor (candidate, pluginType)

        let inst =
            match constructor with
            | PluginArg cons ->
                cons.Invoke [| plugin.Instance |]
            | PluginInfoArg cons ->
                cons.Invoke [| plugin |]
            | ZeroArg cons ->
                cons.Invoke [||]
            | NoneFound ->
                logger.LogWarning $"Invalid entrypoint {candidate.FullName}: no valid constructor"

        { Source = plugin
          Instance = unbox<'t> inst }

    Seq.map construct candidates
    |> Seq.toList

/// Scan all plugins for hook subclasses
let scanAll<'t> (): 't EntryPointContainer list =
    Chainloader.PluginInfos.Values
    |> Seq.collect scan<'t>
    |> Seq.toList
