namespace BaboonAPI.UI.AST

open UnityEngine
open UnityEngine.EventSystems
open UnityEngine.UI

module UIHelper =
    let makeElement<'ui when 'ui :> Component> () =
        let gameObject = GameObject()
        gameObject.AddComponent<'ui>()

type IInterfaceNode =
    abstract Children : IInterfaceNode list
    abstract Make : unit -> UIBehaviour

[<AbstractClass>]
type AbstractInterfaceNode<'ui when 'ui :> UIBehaviour>(attributes: Map<string, string>, children) =
    abstract Make : unit -> 'ui
    abstract ApplyAttributes : element: 'ui -> unit

    default _.ApplyAttributes el =

        ()

    member _.Attributes = attributes

    interface IInterfaceNode with
        member this.Children = children
        member this.Make () = this.Make()

[<AbstractClass>]
type AbstractLayoutGroup<'ui when 'ui :> LayoutGroup>(attributes, children) =
    inherit AbstractInterfaceNode<'ui>(attributes, children)

    default _.ApplyAttributes element =
        element.padding <- RectOffset()
        ()

type HorizontalLayoutNode(attributes, children) =
    inherit AbstractLayoutGroup<HorizontalLayoutGroup>(attributes, children)

    let spacing = float32 attributes["spacing"]

    override this.ApplyAttributes(element) =
        base.ApplyAttributes element
        element.spacing <- spacing

    override this.Make() =
        UIHelper.makeElement()
