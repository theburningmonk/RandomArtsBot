namespace RandomArtsBot

module Responder =
    /// Starts a loop to keep polling for new mentions and responding to them
    val start : botname:string -> unit