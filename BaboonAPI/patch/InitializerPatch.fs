namespace BaboonAPI.Patch

open System.Reflection.Emit
open BaboonAPI.Hooks.Initializer
open BaboonAPI.Utility.Coroutines
open HarmonyLib
open UnityEngine
open UnityEngine.UI

module internal ModInitializer =
    type InitializerState =
        | Success
        | Failed
        | NotRun

    let mutable initialized = NotRun

    let showResult (bc: BrandingController) (result: Result<unit, LoadError>) = coroutine {
        let failtxt = bc.failed_to_load_error.transform.Find("full_text").GetComponent<Text>()

        match result with
        | Ok () ->
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
            rect.offsetMin <- Vector2(10f, 30f)
            rect.offsetMax <- Vector2(250f, 30f)

            initialized <- Success
            if GlobalVariables.skipbrandingscreen then
                bc.Invoke("killandload", 0.75f)
            else
                bc.Invoke("doHolyWowAnim", 0.75f)
        | Error err ->
            initialized <- Failed

            let meta = err.PluginInfo.Metadata
            failtxt.verticalOverflow <- VerticalWrapMode.Overflow
            failtxt.alignment <- TextAnchor.UpperCenter
            failtxt.text <- String.concat "\n" [
                "<size=27>Mod initialization failure!</size>"
                $"There was an error loading mod <color=#F3385A>{meta.Name}</color> {meta.Version}:"
                ""
                $"<color=#CCCCCC>{err.Message}</color>"
                ""
                "Make sure your mods are up to date too!"
                $"(source: <color=#23FFFF>{err.PluginInfo.Location}</color>)"
            ]

            bc.epwarningcg.gameObject.SetActive false
            bc.failed_to_load_error.SetActive true

        ()
    }

    let Initialize (bc: BrandingController) =
        coroutine {
            match GameInitializationEvent.EVENT.invoker.Initialize() with
            | Ok () ->
                let! asyncResult = AsyncGameInitializationEvent.EVENT.invoker.Initialize()
                yield bc.StartCoroutine(showResult bc asyncResult)
            | Error err ->
                yield bc.StartCoroutine(showResult bc (Error err))
        }

    let GetStatus () = initialized

/// Safeguard prefix patches that try to cover for other patch errors
[<HarmonyPatch(typeof<BrandingController>)>]
type SafeguardPatch() =
    [<HarmonyPatch("Start")>]
    [<HarmonyPrefix>]
    static member Prefix (__instance: BrandingController) =
        let failtxt = __instance.failed_to_load_error.transform.Find("full_text").GetComponent<Text>()
        failtxt.verticalOverflow <- VerticalWrapMode.Overflow
        failtxt.alignment <- TextAnchor.UpperLeft
        failtxt.rectTransform.sizeDelta <- Vector2(980f, 365f)
        failtxt.text <- String.concat "\n" [
            "<size=27>Uh oh</size>"
            "BaboonAPI's initializer event didn't fire! One of several things may have happened:"
            "1. Trombone Champ updated and our initializer patch broke"
            "2. Another mod is interfering with BaboonAPI, probably by accident"
            "3. The evil doppelgänger <color=#F3385A>Trazom</color> is attempting to break your game"
            ""
            "Try updating your mods or disabling some temporarily?"
        ]

        true

    [<HarmonyPatch("killandload")>]
    [<HarmonyPrefix>]
    static member KillPrefix (__instance: BrandingController) =
        // If initialization fails, don't run killandload
        // Extra safeguard against mods calling killandload or base game changes
        match ModInitializer.GetStatus() with
        | ModInitializer.Success -> true
        | ModInitializer.Failed -> false
        | ModInitializer.NotRun ->
            __instance.failed_to_load_error.SetActive true
            false

[<HarmonyPatch(typeof<BrandingController>)>]
type BrandingPatch() =
    static let loadtracks_m = AccessTools.Method(typeof<TrackCollections>, "buildTrackCollections")

    static member RunInitialize (instance: BrandingController) =
        ModInitializer.Initialize instance
        |> instance.StartCoroutine
        |> ignore

    // Remove TrackCollections.buildTrackCollections() call, we need to patch it first
    [<HarmonyPatch("Start")>]
    [<HarmonyTranspiler>]
    static member PatchStart (instructions: CodeInstruction seq): CodeInstruction seq =
        CodeMatcher(instructions)
            .MatchForward(false, [|
                CodeMatch OpCodes.Ldarg_0
                CodeMatch OpCodes.Ldfld
                CodeMatch OpCodes.Ldc_I4_0
                CodeMatch (fun ins -> ins.Calls loadtracks_m)
            |])
            .ThrowIfInvalid("Could not find TrackCollections#buildTrackCollections")
            // Run our initializer in here instead
            .Advance(1)
            .RemoveInstructions(2)
            .Set(OpCodes.Call, AccessTools.Method(typeof<BrandingPatch>, "RunInitialize"))
            .MatchForward(false, [|
                CodeMatch OpCodes.Ldarg_0
                CodeMatch (OpCodes.Ldstr, "doHolyWowAnim")
                CodeMatch (fun ins -> ins.LoadsConstant())
                CodeMatch OpCodes.Call
            |])
            .ThrowIfInvalid("Could not find Invoke(\"doHolyWowAnim\")")
            .SetAndAdvance(OpCodes.Ret, null)
            .RemoveInstructions(3)
            .InstructionEnumeration()
