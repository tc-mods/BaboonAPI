namespace rec BaboonAPI.UI.AST

open UnityEngine
open UnityEngine.UI

module InterfaceBuilder =
    let private makeElement<'ui when 'ui :> Component> () =
        let gameObject = GameObject()
        gameObject.AddComponent<'ui>()

    let private setParent (parent: Component) (child: Component) =
        child.transform.SetParent parent.transform

    let rec buildTree (root: ElementType) : Component =
        match root with
        | HorizontalGroup(attrs, children) ->
            let ui = makeElement<HorizontalLayoutGroup>()
            ui.padding <- attrs.padding

            Seq.map buildTree children
            |> Seq.iter (setParent ui)

            ui
        | VerticalGroup(attrs, children) ->
            let ui = makeElement<VerticalLayoutGroup>()
            ui.padding <- attrs.padding

            Seq.map buildTree children
            |> Seq.iter (setParent ui)

            ui
        | Text(attrs, content) ->
            let ui = makeElement<UnityEngine.UI.Text>()
            ui.alignment <- attrs.align
            ui.color <- attrs.color
            ui.fontSize <- attrs.fontSize
            ui.text <- content

            ui
        | Image src ->
            let ui = makeElement<UnityEngine.UI.Image>()
            // TODO ui.sprite <- src

            ui

    let buildLayout (roots: ElementType list) =
        let canvas = makeElement<Canvas>()
        canvas.renderMode <- RenderMode.ScreenSpaceOverlay

        roots |> Seq.map buildTree |> Seq.iter (setParent canvas)
        canvas

type LayoutGroupAttrs = { padding: RectOffset }
type TextAttrs = { align: TextAnchor; fontSize: int; color: Color32; }

type ElementType =
    | HorizontalGroup of attrs: LayoutGroupAttrs * ElementType list
    | VerticalGroup of attrs: LayoutGroupAttrs * ElementType list
    | Text of attrs: TextAttrs * content: string
    | Image of src: string
