namespace RandomArtsBot

module Twitter =
    open System
    open LinqToTwitter

    type Id      = uint64
    type Message = string

    type User =
        {
            Id             : Id
            Name           : string
            Handle         : string
            Description    : string
            FollowersCount : int
            FriendsCount   : int
            FavoritesCount : int
            Location       : string
        }

    type Interaction =
        | Mention of User * Id * Message * createdAt:DateTime
        | DM      of User * Id * Message * createdAt:DateTime
        | Retweet of User * Id * createdAt:DateTime
        | Fav     of User * Id * createdAt:DateTime

        member Id        : Id
        member User      : User
        member Timestamp : DateTime

    type Response = 
        {
            SenderHandle    : string
            SenderMessage   : string
            SenderMessageId : Id
            Reply           : string
            MediaIds        : Id list
        }

    type Tweet =
        {
            Message  : string
            MediaIds : Id list
        }

    /// Returns a list of new DMs since the specified `sinceId`. If `sinceId` is
    /// not specified then all DMs are returned
    val pullDMs : sinceId:Id option -> Interaction list * nextId:Id option * TimeSpan

    /// Returns a list of new mentions since the specified `sinceId`. If `sinceId`
    /// is not specified then all mentions are returned
    val pullMentions : sinceId:Id option -> Interaction list * nextId:Id option * TimeSpan

    /// Uploads an image to Twitter by local path. Returns an Image ID that can
    /// be included in a response message
    val uploadImage : string -> Async<Id>

    /// Sends a response
    val send : Response -> Async<unit>

    /// Tweets a new tweet
    val tweet : Tweet -> Async<unit>

    /// Follows a twitter user
    val follow : Id -> Async<unit>