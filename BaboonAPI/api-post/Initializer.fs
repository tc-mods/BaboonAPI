namespace BaboonAPI.Hooks.Initializer

open BaboonAPI.Patch

/// Functions to query the state of mod initialization
module BaboonInitializer =
    /// True if BaboonAPI successfully initialized all mods,
    /// false if initialization failed or hasn't run yet
    let public IsInitialized () = ModInitializer.IsInitialized()
