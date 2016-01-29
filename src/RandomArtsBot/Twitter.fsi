namespace RandomArtsBot

module Twitter =
    open System
    open System.Drawing
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

    type InteractionKind =
        | Mention of Message
        | DM      of Message
        | Retweet
        | Fav

    type Interaction =
        { 
            Id        : Id
            User      : User
            CreatedAt : DateTime
            Kind      : InteractionKind
        }

    type ResponseKind =
        | Tweet
        | DM

    type Response = 
        {
            SenderHandle    : string
            SenderMessage   : string
            SenderMessageId : Id
            Kind            : ResponseKind
            Reply           : string
            Media           : (Bitmap * Id) list
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

    /// Uploads an image to Twitter. Returns an Image ID that can be included in a 
    /// response message
    val uploadImage : Bitmap -> Async<Id>

    /// Sends a response
    val send : Response -> Async<unit>

    /// Tweets a new tweet
    val tweet : Tweet -> Async<unit>

    /// Follows a twitter user
    val follow : Id -> Async<unit>