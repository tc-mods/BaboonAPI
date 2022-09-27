namespace BaboonAPI.Patch

open System.Collections
open System.Reflection.Emit
open BaboonAPI.Internal
open HarmonyLib
open UnityEngine

module SeqUtil =
    let partition predicate (s: seq<'a>) =
        let i = s |> Seq.findIndex predicate
        Seq.take i s, Seq.item i s, Seq.skip (i + 1) s

type GameControllerExtension() =
    static member Infix (instance: GameController) =
        let name = if instance.freeplay then "freeplay" else (TrackAccessor.fetchTrackByIndex instance.levelnum).trackref

        let bundle = AssetBundle.LoadFromFile $"{Application.dataPath}/StreamingAssets/trackassets/{name}"
        ()

[<HarmonyPatch(typeof<GameController>, "Start")>]
type GameControllerPatch() =
    static let startcoro_m = AccessTools.Method (typeof<MonoBehaviour>, "StartCoroutine", [| typeof<IEnumerator> |])

    static member Transpile (instructions: CodeInstruction seq): CodeInstruction seq = seq {
        let head, _, tail = instructions |> SeqUtil.partition (fun ins -> ins.Is(OpCodes.Ldstr, "/StreamingAssets/trackassets/"))
        yield! head

        yield CodeInstruction OpCodes.Ldarg_0
        yield CodeInstruction.Call (typeof<GameControllerExtension>, "Infix")

        let _, _, rest = tail |> SeqUtil.partition (fun ins -> ins.Calls startcoro_m)

        // skip the pop
        yield! rest |> Seq.tail
    }
