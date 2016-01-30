namespace RandomArtsBot

open System

type Speaker = 
    | Us | Them

type IState =
    /// returns the converstions with this recipient so far
    abstract member GetConvo : string -> Async<seq<DateTime * Speaker * string>>

    /// add lines to an ongoing conversation with a recipient
    abstract member AddConvo : string * seq<DateTime * Speaker * string> -> Async<unit>

    /// returns the ID of the last DM that had been processed
    abstract member LastMessage : string -> Async<Id option>

    /// updates the ID of the last DM that had been processed
    abstract member UpdateLastMessage : string * Id -> Async<unit>

    /// returns the ID of the last mention that had been processed
    abstract member LastMention : string -> Async<Id option>

    /// updates the ID of the last mention that had been processed
    abstract member UpdateLastMention : string * Id -> Async<unit>

    /// atomically save an expr
    abstract member AtomicSave : Expr -> Async<bool>

type State =
    new : unit -> State

    interface IState