namespace BaboonAPI

open System.Reflection
open BaboonAPI.Internal.Scores
open BaboonAPI.Patch
open BepInEx
open HarmonyLib

[<BepInPlugin("ch.offbeatwit.baboonapi.plugin", "BaboonAPI", "2.0.0.0")>]
type BaboonPlugin() =
    inherit BaseUnityPlugin()

    member this.Awake() =
        this.Logger.LogInfo "Hello from BaboonAPI!"

        Harmony.CreateAndPatchAll(Assembly.GetExecutingAssembly(), "ch.offbeatwit.baboonapi.plugin") |> ignore

        ScoreSaver.setup this.Info
        ScoreSaverPatch.setup ()
        ()
