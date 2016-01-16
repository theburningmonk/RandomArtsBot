namespace RandomArtsBot

module Twitter =
    open System
    open LinqToTwitter

    type SinceID  = uint64
    type StatusID = uint64
    type MediaID  = uint64

    type Response = 
        {
            RecipientName : string
            StatusID      : StatusID
            Message       : string
            MediaIDs      : MediaID list
        }

    val pullMentions : SinceID option -> (Status list * SinceID option * TimeSpan)

    /// Uploads an image to Twitter by local path. Returns an Image ID that can
    /// be included in a response message
    val uploadImage : string -> Async<MediaID>

    /// Sends a response
    val send : Response -> Async<unit>