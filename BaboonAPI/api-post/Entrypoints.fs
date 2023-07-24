module BaboonAPI.Hooks.Entrypoints.Entrypoints

open System.Collections.Generic
open BaboonAPI.Internal

/// <summary>Get a list of entrypoint containers for the specified type</summary>
/// <remarks>
/// This call is expensive! You should not be fetching entrypoints in a loop.
/// Instead, do it once and store the result.
/// 
/// Calling this method will search for any subclasses of 't with the
/// <see cref="T:BaboonAPI.Hooks.Entrypoints.BaboonEntryPointAttribute">BaboonEntryPoint</see> attribute and
/// instantiate them.
/// </remarks>
let public getEntrypointContainers<'t> (): 't EntryPointContainer IReadOnlyList =
    EntryPointScanner.scanAll<'t>()

/// <summary>Get a list of entrypoints of the specified type</summary>
/// <remarks>
/// This call is expensive! You should not be fetching entrypoints in a loop.
/// Instead, do it once and store the result.
///
/// Calling this method will search for any subclasses of 't with the
/// <see cref="T:BaboonAPI.Hooks.Entrypoints.BaboonEntryPointAttribute">BaboonEntryPoint</see> attribute and
/// instantiate them.
/// </remarks>
let public get<'t> (): 't IReadOnlyList =
    EntryPointScanner.scanAll<'t>()
    |> List.map (fun c -> c.Instance)
    :> 't IReadOnlyList
