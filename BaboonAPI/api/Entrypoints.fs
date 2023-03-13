namespace BaboonAPI.Hooks.Entrypoints

open System
open BepInEx

/// Marks a class as an "entrypoint": a subclass that may be
/// instantiated by another plugin at will.
type public BaboonEntryPointAttribute() =
    inherit Attribute()

/// Holds a specific entrypoint instance and the plugin that owns it
type public EntryPointContainer<'t> =
    {
      // Plugin that provided this entrypoint
      Source: PluginInfo

      // Entrypoint instance
      Instance: 't }
