module BaboonAPI.Utility.Coroutines

open System
open System.Threading.Tasks
open UnityEngine

/// <summary>Async operation type</summary>
/// <remarks>
/// Can be run inside coroutine computation expressions:
/// <code lang="fsharp">coroutine {
///  let! assetBundle = openAssetBundleFromFile "mybundle.assetbundle"
///  printf $"Loaded {assetBundle.name}"
///}</code>
/// </remarks>
type YieldTask<'r> internal (yi: YieldInstruction, supplier: unit -> 'r) =
    member internal _.Coroutine = yi
    member internal _.Result : 'r = supplier()

    /// <summary>
    /// Attach a callback to this YieldTask which will run with its result
    /// </summary>
    /// <remarks>
    /// The returned IEnumerator must be yielded to Unity, otherwise the callback will never run.
    /// </remarks>
    /// <example>
    /// Usage example within a coroutine callback:
    /// <code lang="csharp">public IEnumerator Start()
    ///{
    ///    var task = Unity.loadAudioClip("sound.ogg", AudioType.OGGVORBIS);
    ///    return task.ForEach(result => Debug.Log("Loaded clip!"));
    ///}</code>
    /// </example>
    /// <param name="action">Consumer that will receive the task's result</param>
    member public this.ForEach (action: Action<'r>) =
        (seq {
            yield this.Coroutine
            action.Invoke this.Result
        }).GetEnumerator()

    /// <summary>
    /// Project the result of this YieldTask into a new form
    /// </summary>
    /// <param name="selector">A transformation function to apply to the result of this task</param>
    member public this.Select (selector: Func<'r, 'u>) =
        YieldTask (this.Coroutine, fun () -> selector.Invoke this.Result)

/// Await an AsyncOperation
let awaitAsyncOperation<'r, 'op when 'op :> AsyncOperation> (binder: 'op -> 'r) (op: 'op) =
    YieldTask(op, fun () -> binder op)

/// Await an AssetBundleCreateRequest, returning the loaded AssetBundle
let public awaitAssetBundle : op: AssetBundleCreateRequest -> _ =
    awaitAsyncOperation (fun op -> op.assetBundle)

/// Await a ResourceRequest, returning the loaded Unity Object
let public awaitResource : op: ResourceRequest -> _ =
    awaitAsyncOperation (fun op -> op.asset)

/// Create a YieldTask that returns its result immediately
let public sync (cb: unit -> 't) = YieldTask(null, cb)

type CoroutineBuilder() =
    member _.Yield (yi: YieldInstruction) = Seq.singleton yi
    member _.Yield (yi: CustomYieldInstruction) =
        seq {
            while yi.keepWaiting do
                yield yi.Current :?> YieldInstruction
        }

    member _.YieldFrom (syi: YieldInstruction seq) = syi

    member _.Bind (src: YieldTask<'a>, binder: 'a -> YieldInstruction seq) =
        seq {
            yield src.Coroutine // run the coroutine
            yield! binder(src.Result) // then call the binder with the result
        }

    member _.Using<'a when 'a :> IDisposable> (expr: 'a, binder: 'a -> YieldInstruction seq) =
        seq {
            try
                yield! binder(expr)
            finally
                expr.Dispose()
        }

    member _.For (expr: 'a seq, binder: 'a -> YieldInstruction seq) = Seq.collect binder expr

    member _.While (predicate: unit -> bool, body: YieldInstruction seq) =
        seq {
            while predicate() do
                yield! body
        }

    member _.Combine (a: YieldInstruction seq, b: YieldInstruction seq) = Seq.append a b

    member _.Delay (binder: unit -> YieldInstruction seq) = Seq.delay binder

    member _.Zero () : YieldInstruction seq = Seq.empty

    member _.Run (result: YieldInstruction seq) = result.GetEnumerator()

/// Unity coroutine computation expression
let coroutine = CoroutineBuilder()

/// Transform a YieldTask
let map (binder: 'a -> 'b) (task: YieldTask<'a>): YieldTask<'b> =
    YieldTask(task.Coroutine, fun () -> binder task.Result)

/// Consume a YieldTask into an IEnumerator, allowing it to be started as a Unity coroutine
let run (task: YieldTask<unit>) =
    (seq {
        yield task.Coroutine
        task.Result
    }).GetEnumerator()

/// <summary>Register a callback that is called after the YieldTask is run by Unity.</summary>
/// <remarks>This function returns an IEnumerator that must be passed to Unity before the callback will run!</remarks>
let each (runner: 'a -> unit) (task: YieldTask<'a>) =
    task
    |> map runner
    |> run

/// Suspends the coroutine until the given .NET task completes
type WaitForTask<'a> (task: Task<'a>) =
    inherit CustomYieldInstruction()

    override this.keepWaiting = not task.IsCompleted
