namespace RandomArtsBot

open System
open RandomArt
open Twitter

module State =
    type Speaker = 
        | Us | Them

    /// returns the converstions with this recipient so far
    val getConvo : string -> Async<seq<DateTime * Speaker * string>>

    /// add lines to an ongoing conversation with a recipient
    val addConvo : string -> seq<DateTime * Speaker * string> -> Async<unit>

    /// returns the ID of the last DM that had been processed
    val lastMessage : string -> Async<Id option>

    /// updates the ID of the last DM that had been processed
    val updateLastMessage : string -> Id -> Async<unit>

    /// returns the ID of the last mention that had been processed
    val lastMention : string -> Async<Id option>

    /// updates the ID of the last mention that had been processed
    val updateLastMention : string -> Id -> Async<unit>

    /// atomically save an expr
    val atomicSave : Expr -> Async<bool>