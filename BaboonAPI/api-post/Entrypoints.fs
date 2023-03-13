module BaboonAPI.Hooks.Entrypoints.Entrypoints

open BaboonAPI.Internal

let getEntrypointContainers<'t> (): 't EntryPointContainer list =
    EntryPointScanner.scanAll<'t>()
