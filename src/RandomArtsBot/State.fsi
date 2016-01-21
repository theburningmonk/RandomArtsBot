namespace RandomArtsBot

open System
open Twitter

module State =
    type Speaker = 
        | Us | Them

    /// returns the converstions with this recipient so far
    val getConvo : string -> (DateTime * Speaker * string)[]

    /// add lines to an ongoing conversation with a recipient
    val addConvo : string -> seq<DateTime * Speaker * string> -> unit

    /// returns the ID of the last mention that had been processed
    val lastMention : string -> Async<StatusID option>

    /// updates the ID of the last mention that had been processed
    val updateLastMention : string -> StatusID -> Async<unit>