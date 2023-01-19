module BaboonAPI.Internal.Coroutines

open System
open UnityEngine

type IYieldWithResult<'r> =
    abstract Coroutine : YieldInstruction
    abstract Result : 'r

/// Await an AsyncOperation
let awaitAsyncOperation<'r, 'op when 'op :> AsyncOperation> (binder: 'op -> 'r) (op: 'op) =
    { new IYieldWithResult<'r> with
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

    member _.Bind (src: IYieldWithResult<'a>, binder: 'a -> YieldInstruction seq) =
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

let coroutine = CoroutineBuilder()
