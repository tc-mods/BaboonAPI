module BaboonAPI.UI.AST.Parser

open System
open System.Globalization
open System.Xml.Linq
open UnityEngine

let parseAttribute (name: string) (el: XElement) =
    el.Attribute(name).Value

let parsePadding (el: XElement) =
    let input = parseAttribute "padding" el
    let parts =
        input.Split [| ' ' |]
        |> Array.map int
    match parts with
    | [| a |] -> RectOffset(a, a, a, a)
    | [| v; h |] -> RectOffset(h, h, v, v)
    | [| t; h; b |] -> RectOffset(h, h, t, b)
    | [| t; r; b; l |] -> RectOffset(l, r, t, b)
    | _ -> failwith "invalid padding attribute"

let parseAlignment (el: XElement) =
    let input = parseAttribute "align" el
    match input with
    | "top-left" -> TextAnchor.UpperLeft
    | "top" -> TextAnchor.UpperCenter
    | "top-right" -> TextAnchor.UpperRight
    | "left" -> TextAnchor.MiddleLeft
    | "center" -> TextAnchor.MiddleCenter
    | "right" -> TextAnchor.MiddleRight
    | "bottom-left" -> TextAnchor.LowerLeft
    | "bottom" -> TextAnchor.LowerCenter
    | "bottom-right" -> TextAnchor.LowerRight
    | _ -> failwith "invalid align attribute"

let parseColor (el: XElement) =
    let input = parseAttribute "color" el
    let rgb = Int32.Parse(input, NumberStyles.HexNumber)
    let r, g, b = (rgb >>> 16, rgb >>> 8 &&& 0xFF, rgb &&& 0xFF)

    Color32(byte r, byte g, byte b, 255uy)

let rec parseElement (el: XElement) =
    match el.Name.LocalName with
    | "HorizontalGroup" ->
        let children = Seq.map parseElement (el.Elements())
        ElementType.HorizontalGroup ({ padding = parsePadding el }, Seq.toList children)
    | "VerticalGroup" ->
        let children = Seq.map parseElement (el.Elements())
        ElementType.VerticalGroup ({ padding = parsePadding el }, Seq.toList children)
    | "Text" ->
        ElementType.Text ({
            align = parseAlignment el
            color = parseColor el
            fontSize = (parseAttribute "fontSize" el |> int)
        }, el.Value)
    | _ -> failwith "invalid element"

let parse (content: string) =
    let layout = XDocument.Parse content

    let children =
        layout.Elements()
        |> Seq.map parseElement
        |> Seq.toList

    children
