namespace RandomArtsBot

type IResponder =
    /// Starts a loop to keep polling for new mentions and responding to them
    abstract member Start : botname:string -> unit

type Responder =
    new : ITwitterClient * IArtist * IState -> Responder

    interface IResponder