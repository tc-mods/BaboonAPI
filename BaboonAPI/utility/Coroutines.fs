module BaboonAPI.Utility.Coroutines

open System
open UnityEngine

/// <summary>Async operation type</summary>
/// <remarks>
/// Can be run inside coroutine computation expressions:
/// <code lang="fsharp">coroutine {
///  let! assetBundle = openAssetBundleFromFile "mybundle.assetbundle"
///  printf $"Loaded {assetBundle.name}"
///}</code>
/// </remarks>
type YieldTask<'r> =
    abstract Coroutine : YieldInstruction
    abstract Result : 'r

/// Await an AsyncOperation
let awaitAsyncOperation<'r, 'op when 'op :> AsyncOperation> (binder: 'op -> 'r) (op: 'op) =
    { new YieldTask<'r> with
        member _.Coroutine = op
        member _.Result = binder op }

/// Await an AssetBundleCreateRequest, returning the loaded AssetBundle
let public awaitAssetBundle : op: AssetBundleCreateRequest -> _ =
    awaitAsyncOperation (fun op -> op.assetBundle)

/// Await a ResourceRequest, returning the loaded Unity Object
let public awaitResource : op: ResourceRequest -> _ =
    awaitAsyncOperation (fun op -> op.asset)

type CoroutineBuilder() =
    member _.Yield (yi: YieldInstruction) = Seq.singleton yi

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

    member _.Combine (a: YieldInstruction seq, b: YieldInstruction seq) = Seq.append a b

    member _.Delay (binder: unit -> YieldInstruction seq) = Seq.delay binder

    member _.Zero () : YieldInstruction seq = Seq.empty

    member _.Run (result: YieldInstruction seq) = result.GetEnumerator()

/// Unity coroutine computation expression
let coroutine = CoroutineBuilder()

/// Transform a YieldTask
let map (binder: 'a -> 'b) (task: YieldTask<'a>): YieldTask<'b> =
    { new YieldTask<'b> with
        member _.Coroutine = task.Coroutine
        member _.Result = binder task.Result }

/// Consume a YieldTask into an IEnumerator, allowing it to be started as a Unity coroutine
let run (task: YieldTask<'a>) =
    (Seq.delay (fun () -> Seq.singleton task.Coroutine)).GetEnumerator()
