﻿module internal BaboonAPI.Internal.EntryPointScanner

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

    let types =
        try
            assembly.GetTypes()
        with
        | :? ReflectionTypeLoadException as exc ->
            exc.Types |> Array.filter (isNull >> not)

    let candidates =
        types
        |> Seq.filter target.IsAssignableFrom
        |> Seq.filter (getCustomAttribute<BaboonEntryPointAttribute> >> Option.isSome)

    let construct (candidate: Type) =
        let constructor = findConstructor (candidate, pluginType)

        let inst =
            match constructor with
            | PluginArg cons ->
                Some (cons.Invoke [| plugin.Instance |])
            | PluginInfoArg cons ->
                Some (cons.Invoke [| plugin |])
            | ZeroArg cons ->
                Some (cons.Invoke [||])
            | NoneFound ->
                logger.LogWarning $"Invalid entrypoint {candidate.FullName}: no valid constructor"
                None

        inst
        |> Option.map (fun instance ->
            { Source = plugin; Instance = unbox<'t> instance })

    Seq.map construct candidates
    |> Seq.choose id
    |> Seq.toList

/// Scan all plugins for hook subclasses
let scanAll<'t> (): 't EntryPointContainer list =
    Chainloader.PluginInfos.Values
    |> Seq.collect scan<'t>
    |> Seq.toList
