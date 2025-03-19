namespace BaboonAPI.Hooks.Initializer

open System
open BaboonAPI.Event
open BaboonAPI.Utility
open BaboonAPI.Utility.Coroutines
open BepInEx
open Microsoft.FSharp.Core
open UnityEngine

module private ResultExt =
    let runUntilErr (s: Result<unit, 'e> seq): Result<unit, 'e> =
        use e = s.GetEnumerator()
        let mutable error = None

        while Option.isNone error && e.MoveNext() do
            match e.Current with
            | Ok _ -> ()
            | Error err -> error <- Some err

        match error with
        | Some err -> Error err
        | None -> Ok ()

    let runAsync (tasks: YieldTask<Result<unit, 'err>> seq) : YieldTask<Result<unit, 'err>> =
        let mutable error = None

        let coro =
            Unity.task {
                for entry in tasks do
                    if Option.isNone error then
                        match! entry with
                        | Ok () -> ()
                        | Error err -> error <- Some err
            }

        coro |> map (fun () ->
            match error with
            | None -> Ok()
            | Some error -> Error error)

/// Load error returned by plugins when they fail to load.
type LoadError = { PluginInfo: PluginInfo
                   Message: string }

/// <namespacedoc>
/// <summary>Safe mod initialization hooks</summary>
/// </namespacedoc>
///
/// <summary>Game initialization event</summary>
/// <remarks>
/// Using the listener interface and the
/// <see cref="M:BaboonAPI.Hooks.Initializer.GameInitializationEvent.attempt(BepInEx.PluginInfo,Microsoft.FSharp.Core.FSharpFunc{Microsoft.FSharp.Core.Unit,Microsoft.FSharp.Core.Unit})">attempt</see>
/// method, you can perform fallible setup tasks that will be safely reported to the user if they go wrong.
/// </remarks>
/// <example>
///<code lang="fsharp">member this.Awake() =
///    GameInitializationEvent.EVENT.Register this
///
///interface GameInitializationEvent.Listener with
///    member this.Initialize() =
///        GameInitializationEvent.attempt this.Info (fun () ->
///            // fallible logic goes here.
///        )
///</code>
/// </example>
module GameInitializationEvent =
    /// Initialization event listener
    type Listener =
        /// <summary>Initialization callback</summary>
        /// <returns>Initialization result, see
        /// <see cref="M:BaboonAPI.Hooks.Initializer.GameInitializationEvent.attempt(BepInEx.PluginInfo,Microsoft.FSharp.Core.FSharpFunc{Microsoft.FSharp.Core.Unit,Microsoft.FSharp.Core.Unit})">attempt</see>
        /// </returns>
        abstract Initialize: unit -> Result<unit, LoadError>

    let rec internal formatError (err: exn) =
        let inner =
            Option.ofObj err.InnerException
            |> Option.map (fun i -> $"\n{formatError i}")
            |> Option.defaultValue ""

        $"{err.Message}{inner}"

    /// <summary>Wraps your initialization logic and catches any thrown exceptions.</summary>
    /// <remarks>
    /// An exception thrown in here will safely stop the game loading, and the error will be displayed to the user.
    /// </remarks>
    /// <param name="info">Metadata used when displaying errors</param>
    /// <param name="applier">Wrapped function</param>
    let public attempt (info: PluginInfo) (applier: unit -> unit) =
        try
            Ok(applier())
        with
        | err ->
            Debug.LogError err
            Error { PluginInfo = info; Message = (formatError err) }

    /// Event bus
    let EVENT = EventFactory.create(fun listeners ->
        { new Listener with
            override _.Initialize () =
                listeners
                |> Seq.map (fun l -> l.Initialize())
                |> ResultExt.runUntilErr })

    /// <summary>Helper function to quickly register a fallible initialization callback from C#</summary>
    /// <remarks>
    /// This function lets you easily set up an initialization callback in C# without having to convert to F# functions
    /// manually.
    ///
    /// An exception thrown in here will safely stop the game loading, and the error will be displayed to the user.
    /// </remarks>
    /// <param name="info">Metadata used when displaying errors</param>
    /// <param name="applier">Wrapped function</param>
    let public Register (info: PluginInfo) (applier: Action) =
        EVENT.Register { new Listener with
                           override _.Initialize() =
                               attempt info (FuncConvert.FromAction applier) }

/// Async game initialization event
module AsyncGameInitializationEvent =
    type Listener =
        abstract Initialize: unit -> YieldTask<Result<unit, LoadError>>

    let EVENT = EventFactory.create(fun listeners ->
        { new Listener with
            override _.Initialize () =
                listeners
                |> Seq.map (_.Initialize())
                |> ResultExt.runAsync })
