(*** condition: prepare ***)
#I "../BaboonAPI/bin/Debug/net472"
#r "BaboonAPI.dll"
#r "Assembly-CSharp.dll"
#r "UnityEngine.dll"
#r "UnityEngine.CoreModule.dll"
#r "nuget:BepInEx.Core, 5.*"

(**
# Entrypoints

Often you'd like to provide API that other mods can use, but you also don't want them to have a hard dependency on you.
For example, perhaps you have a GUI that you'd like other mods to be able to use, but it's not essential to that other
mod's functioning.

The Entrypoints API provides the tools to do this. It lets other mods use your APIs easily, with full compile-time
type-checking, but without introducing a hard dependency on your mod being present at runtime.
*)

open BaboonAPI.Hooks.Entrypoints

(*** hide ***)
open BaboonAPI.Hooks.Initializer
open BepInEx
open UnityEngine

type CustomTag =
    abstract Remove: unit -> unit

(**
Here's an example. Let's say your mod lets players name their trombone.
You want to let other mods change the name tag, or add additional name tags.

You might have a basic interface type like this:
*)

type NameableTrombone =
    /// Set the name of this trombone
    abstract SetName: name: string -> unit

    /// Get the current name of this trombone
    abstract name: string

    /// Set the color of the name tag on this trombone
    abstract SetTagColor: color: Color -> unit

    /// Get the current color of the name tag on this trombone
    abstract color: Color

    /// Add an extra name tag to this trombone
    abstract AddCustomTag: text: string * color: Color -> CustomTag

(*** hide ***)
type NameableTromboneImpl() =
    interface NameableTrombone with
        member this.AddCustomTag (text, color) = failwith "example implementation"
        member this.SetName name = failwith "example implementation"
        member this.SetTagColor color = failwith "example implementation"
        member this.color = failwith "example implementation"
        member this.name = failwith "example implementation"

(**
Then you need a way for other mods to get a NameableTrombone instance. So you add some static method somewhere:
*)

module TromboneLookup =
    let public getTrombone(): NameableTrombone =
        // Just return an example type...
        NameableTromboneImpl()

(**
However, this presents a problem. Any mod trying to use this lookup API now has a hard dependency on your mod.
*)

// Some cool downstream mod
[<BepInPlugin("CoolPlugin", "CoolPlugin", "1.0.0")>]
type CoolPlugin() =
    inherit BaseUnityPlugin()

    member _.Awake() =
        // Hard dependency, type must be available at runtime.
        let trombone = TromboneLookup.getTrombone()
        trombone.SetName "My Cool Trombone"

(**
Putting the call behind an if-mod-present check won't work either, because your mod will still attempt to resolve the
`TromboneLookup` type when the class is loaded, which will trigger an exception if the dependency is missing.

This is what the Entrypoints API intends to fix!

## Fixing it

Entrypoints fixes this problem by *inverting control*. The library mod is responsible for querying the entrypoints API;
the consumers just need to advertise their availability.

Let's demonstrate using our example above. Instead of the TromboneLookup type, we need a new interface:
*)

type TromboneNameListener =
    /// Called when we want to name a trombone
    abstract OnTromboneCreated: NameableTrombone -> unit

(**
This is our "entrypoint listener" type. Consumers then implement this listener on a new type:
*)

// back in our cool downstream mod

[<BaboonEntryPoint>]
type CoolPluginNameListener(plugin: CoolPlugin) =
    interface TromboneNameListener with
        member this.OnTromboneCreated trombone =
            // Now we have access to the NameableTrombone instance!
            trombone.SetName "My Cool Trombone"

(**
Breaking this down:

- we annotate the type with the `cref:T:BaboonAPI.Hooks.Entrypoints.BaboonEntryPointAttribute`
- we (optionally) define a constructor that accepts our Plugin instance
- we implement the "listener interface" from the library plugin

That's all we need to do in the downstream mod! The last step is to head back to our upstream library mod and actually
invoke the entrypoints API:
*)

[<BepInPlugin("TromboneNameTags", "TromboneNameTags", "1.0.0")>]
type LibraryMod() =
    inherit BaseUnityPlugin()

    let trombone = NameableTromboneImpl()

    member _.TryInitialize () =
        // Here we go!
        let listeners = Entrypoints.get<TromboneNameListener>()
        for l in listeners do
            l.OnTromboneCreated trombone
        ()

    interface GameInitializationEvent.Listener with
        member this.Initialize() =
            GameInitializationEvent.attempt this.Info this.TryInitialize

(**
That's it! By calling ```cref:M:BaboonAPI.Hooks.Entrypoints.Entrypoints.get``1```, you scan all the other loaded mods for
types that 1) inherit the specified listener type and 2) are tagged with the BaboonEntryPoint attribute. These types
are then constructed and returned.

Because the entrypoint loader is only called by the library mod, the entrypoints aren't even constructed if the library
mod isn't present, so there's no chance for type-loading errors!

### Things not to do

In order to ensure that the `CoolPluginNameListener` entrypoint isn't loaded without its needed library mod present at
runtime, you shouldn't reference it in any other parts of your code. Instead, your entrypoint should be the one
referencing and calling into your mod's types.

Similarly, you should make sure you're not passing types that only exist in the library mod to other parts of your code.
All references to the library mod's types should be contained within your entrypoint.
*)
