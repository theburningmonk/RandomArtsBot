namespace RandomArtsBot

module Twitter =
    open System
    open LinqToTwitter

    type StatusID = uint64
    type MediaID  = uint64

    type Response = 
        {
            RecipientName : string
            StatusID      : StatusID
            Message       : string
            MediaIDs      : MediaID list
        }

    type Tweet =
        {
            Message     : string
            MediaIDs    : MediaID list
        }

    val pullMentions : StatusID option -> (Status list * StatusID option * TimeSpan)

    /// Uploads an image to Twitter by local path. Returns an Image ID that can
    /// be included in a response message
    val uploadImage : string -> Async<MediaID>

    /// Sends a response
    val send : Response -> Async<unit>

    /// Tweets a new tweet
    val tweet : Tweet -> Async<unit>