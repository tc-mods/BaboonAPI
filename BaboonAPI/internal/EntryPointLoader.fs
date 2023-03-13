module BaboonAPI.Internal.EntryPointScanner

open System
open System.Reflection
open BaboonAPI.Hooks.Entrypoints
open BepInEx
open BepInEx.Bootstrap

let getCustomAttribute<'t when 't :> Attribute> (target: Type): 't option =
    match box (target.GetCustomAttribute<'t>()) with
    | null -> None
    | x -> Some (unbox x)

exception InvalidEntryPoint of Type

type PossibleConstructors =
    | NoneFound
    | ZeroArg of ConstructorInfo
    | SingleArg of ConstructorInfo

let findConstructor (candidate: Type): PossibleConstructors =
    let singleArg =
        Option.ofObj (candidate.GetConstructor [| typeof<PluginInfo> |])
        |> Option.map SingleArg

    let zeroArg = (fun () ->
        Option.ofObj (candidate.GetConstructor [||])
        |> Option.map ZeroArg)

    singleArg
    |> Option.orElseWith zeroArg
    |> Option.defaultValue NoneFound

/// Scan a plugin for hook subclasses
let scan<'t> (plugin: PluginInfo): 't EntryPointContainer list =
    let target = typeof<'t>
    let assembly = plugin.Instance.GetType().Assembly

    let candidates =
        assembly.GetTypes()
        |> Seq.filter target.IsAssignableFrom
        |> Seq.filter (getCustomAttribute<BaboonEntryPointAttribute> >> Option.isSome)

    let construct (candidate: Type) =
        let constructor = findConstructor candidate

        let inst =
            match constructor with
            | SingleArg cons ->
                cons.Invoke [| plugin |]
            | ZeroArg cons ->
                cons.Invoke [||]
            | NoneFound ->
                raise (InvalidEntryPoint candidate)

        { Source = plugin
          Instance = unbox<'t> inst }

    Seq.map construct candidates
    |> Seq.toList

/// Scan all plugins for hook subclasses
let scanAll<'t> (): 't EntryPointContainer list =
    Chainloader.PluginInfos.Values
    |> Seq.collect scan<'t>
    |> Seq.toList
