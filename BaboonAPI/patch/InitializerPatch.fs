namespace BaboonAPI.Patch

open System.Reflection.Emit
open BaboonAPI.Hooks.Initializer
open BaboonAPI.Internal.Coroutines
open HarmonyLib
open UnityEngine
open UnityEngine.UI

module private ModInitializer =
    let Initialize (bc: BrandingController) = coroutine {
        yield WaitForSeconds(6.5f)

        let title = bc.epwarningtxt1.GetComponent<Text>()
        let desc = bc.epwarningtxt2.GetComponent<Text>()

        title.text <- "Loading mods"
        desc.text <- "Initializing your installed mods..."

        LeanTween.rotateZ(bc.epwarningtxt1, 0f, 0.45f).setEaseOutQuart() |> ignore
        LeanTween.rotateZ(bc.epwarningtxt2, 0f, 0.45f).setEaseOutQuart() |> ignore
        LeanTween.moveLocalX(bc.epwarningtxt1, 0f, 0.45f).setEaseOutQuart() |> ignore
        LeanTween.moveLocalX(bc.epwarningtxt2, 0f, 0.45f).setEaseOutQuart() |> ignore

        let initResult = GameInitializationEvent.EVENT.invoker.Initialize()
        match initResult with
        | Ok _ ->
            desc.text <- "All your mods loaded successfully!\nHappy tooting!"
            yield WaitForSeconds(2.0f)

            LeanTween.rotateZ(bc.epwarningtxt1, 65f, 0.45f).setEaseInQuart() |> ignore
            LeanTween.rotateZ(bc.epwarningtxt2, 65f, 0.45f).setEaseInQuart() |> ignore
            LeanTween.moveLocalX(bc.epwarningtxt1, -1600f, 0.45f).setEaseInQuart() |> ignore
            LeanTween.moveLocalX(bc.epwarningtxt2, 1600f, 0.45f).setEaseInQuart() |> ignore

            bc.Invoke("killandload", 0.45f)
        | Error err ->
            let meta = err.PluginInfo.Metadata
            desc.text <-
                $"There was an error loading {meta.Name} {meta.Version}:

                {err.Message}

                The mod is probably just out of date!
                ({err.PluginInfo.Location})"
            // TODO add quit button

        ()
    }

[<HarmonyPatch(typeof<BrandingController>)>]
type BrandingPatch() =
    static let loadlevel_m = AccessTools.Method(typeof<SaverLoader>, "loadLevelData")

    // Remove SaverLoader.loadLevelData() call, we need to patch it first
    [<HarmonyPatch("Start")>]
    [<HarmonyTranspiler>]
    static member PatchStart (instructions: CodeInstruction seq): CodeInstruction seq =
        CodeMatcher(instructions)
            .MatchForward(false, [|
                CodeMatch (fun ins -> ins.Calls loadlevel_m)
            |])
            .Set(OpCodes.Nop, null)
            .InstructionEnumeration()

    [<HarmonyPatch("doHolyWowAnim")>]
    [<HarmonyTranspiler>]
    static member PatchSequence (instructions: CodeInstruction seq): CodeInstruction seq =
        CodeMatcher(instructions)
            .MatchForward(false, [|
                CodeMatch OpCodes.Ldarg_0
                CodeMatch (OpCodes.Ldstr, "killandload")
                CodeMatch (fun ins -> ins.LoadsConstant())
                CodeMatch OpCodes.Call
            |])
            .RemoveInstructions(4)
            .InstructionEnumeration()

    [<HarmonyPatch("epwarning")>]
    [<HarmonyPostfix>]
    static member WarningPostfix (__instance: BrandingController) =
        ModInitializer.Initialize __instance
        |> __instance.StartCoroutine
        |> ignore
        ()
