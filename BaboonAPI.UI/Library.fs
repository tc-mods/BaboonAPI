namespace BaboonAPI.UI

open BaboonAPI.UI.AST

module Builder =
    let parseAndBuild (xml: string) =
        Parser.parse xml
        |> InterfaceBuilder.buildLayout
