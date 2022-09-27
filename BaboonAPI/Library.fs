namespace BaboonAPI

open System.Reflection
open BepInEx
open HarmonyLib

[<BepInPlugin("ch.offbeatwit.baboonapi.plugin", "BaboonAPI", "1.0.0.0")>]
type BaboonPlugin() =
    inherit BaseUnityPlugin()

    member this.Awake() =
        this.Logger.LogInfo "Hello from BaboonAPI!"

        Harmony.CreateAndPatchAll(Assembly.GetExecutingAssembly(), "ch.offbeatwit.baboonapi.plugin")
