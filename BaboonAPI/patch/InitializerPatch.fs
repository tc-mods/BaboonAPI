namespace BaboonAPI.Patch

open System.Reflection.Emit
open BaboonAPI.Hooks.Initializer
open BaboonAPI.Utility.Coroutines
open HarmonyLib
open UnityEngine
open UnityEngine.UI

module private ModInitializer =
    let Initialize (bc: BrandingController) (failObj: GameObject) = coroutine {
        let failtxt = failObj.GetComponentInChildren<Text>()
        failtxt.verticalOverflow <- VerticalWrapMode.Overflow

        let initResult = GameInitializationEvent.EVENT.invoker.Initialize()
        match initResult with
        | Ok _ ->
            let successTxt = GameObject("BaboonApiModsText")
            let s = successTxt.AddComponent<Text>()
            let rect = successTxt.GetComponent<RectTransform>()
            s.font <- failtxt.font
            s.fontSize <- 20
            s.alignment <- TextAnchor.LowerLeft
            s.text <- "<color=#90EE90>All your mods loaded successfully!\nHappy tooting!</color>"

            rect.SetParent bc.epwarningcg.transform
            rect.pivot <- Vector2(0f, 0f)
            rect.anchorMin <- Vector2(0f, 0f)
            rect.anchorMax <- Vector2(1f, 1f)
            rect.offsetMin <- Vector2(10f, -50f)
            rect.offsetMax <- Vector2(250f, 30f)

            LeanTween.value(-50f, 30f, 0.5f)
                .setEaseInOutQuad()
                .setOnUpdate(fun (value: float32) ->
                    rect.offsetMin <- Vector2(10f, value))
                |> ignore

            LeanTween.value(10f, -500f, 0.5f)
                .setEaseOutQuad()
                .setDelay(5.5f)
                .setOnUpdate(fun (value: float32) ->
                    rect.offsetMin <- Vector2(value, 30f))
                |> ignore

            bc.Invoke("killandload", 6.45f)
        | Error err ->
            let meta = err.PluginInfo.Metadata
            failtxt.text <- String.concat "\n" [
                "<size=27>Mod initialization failure!</size>"
                $"There was an error loading mod <color=#F3385A>{meta.Name}</color> {meta.Version}:"
                ""
                $"<color=#CCCCCC>{err.Message}</color>"
                ""
                "The mod is probably just out of date! Please update it!"
                $"(source: {err.PluginInfo.Location})"
            ]

            bc.epwarningcg.gameObject.SetActive false
            failObj.SetActive true

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
    static member WarningPostfix (__instance: BrandingController, ___failed_to_load_error: GameObject) =
        ModInitializer.Initialize __instance ___failed_to_load_error
        |> __instance.StartCoroutine
        |> ignore
        ()
