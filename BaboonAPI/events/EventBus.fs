namespace BaboonAPI.Event

type Event<'T> =
    abstract member invoker: 'T
    abstract member Register: ('T) -> unit
    abstract member Unregister: ('T) -> unit

type EventFactory<'T when 'T: equality>(reducer: 'T list -> 'T) =
    let mutable handlers: 'T list = []

    interface Event<'T> with
        member this.invoker = reducer (handlers)
        override this.Register (handler: 'T) = handlers <- handler :: handlers

        override this.Unregister (handler: 'T) =
            handlers <-
                handlers
                |> List.filter (fun other -> handler <> other)

    static member create(reducer: 'T list -> 'T) : Event<'T> = EventFactory<'T>(reducer) :> Event<'T>
