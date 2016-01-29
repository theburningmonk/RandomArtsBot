namespace RandomArtsBot

module Twitter =
    open System
    open System.Configuration
    open System.Drawing
    open System.IO
    open System.Text.RegularExpressions

    open Imgur.API.Authentication.Impl
    open Imgur.API.Endpoints.Impl
    open LinqToTwitter
    open log4net

    type Id      = uint64
    type Message = string

    let twitterHandleRegex = Regex("@[a-z0-9_]{1,15}")

    let normalize (text : string) = 
        twitterHandleRegex.Replace(text.ToLower(), "").Trim()

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

        static member Of (status : Status) =
            {
                Id     = uint64 status.User.UserIDResponse
                Name   = status.User.Name 
                Handle = "@" + status.User.ScreenNameResponse
                Description    = status.User.Description 
                FollowersCount = status.User.FollowersCount
                FriendsCount   = status.User.FriendsCount
                FavoritesCount = status.User.FavoritesCount
                Location       = status.User.Location
            }

        static member Of (dm : DirectMessage) =
            {
                Id     = uint64 dm.Sender.UserIDResponse
                Name   = dm.Sender.Name 
                Handle = "@" + dm.Sender.ScreenNameResponse
                Description    = dm.Sender.Description 
                FollowersCount = dm.Sender.FollowersCount
                FriendsCount   = dm.Sender.FriendsCount
                FavoritesCount = dm.Sender.FavoritesCount
                Location       = dm.Sender.Location
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

        override x.ToString() =
            match x.Kind with
            | Mention msg -> 
                sprintf "Mention by %s [%s] [%d]" x.User.Handle msg x.Id
            | DM msg ->
                sprintf "DM by %s [%s] [%d]" x.User.Handle msg x.Id
            | Retweet ->
                sprintf "Retweet by %s" x.User.Handle
            | Fav ->
                sprintf "Fav by %s" x.User.Handle

        static member Of (status : Status) =
            match status.Type with
            | StatusType.Mentions -> Mention <| normalize status.Text
            | StatusType.Retweets 
            | StatusType.RetweetsOfMe -> Retweet
            |> (fun kind -> 
                {
                    Id = status.StatusID
                    User = User.Of status
                    CreatedAt = status.CreatedAt
                    Kind = kind
                })

        static member Of (dm : DirectMessage) =
            {
                Id = dm.IDResponse
                User = User.Of dm
                CreatedAt = dm.CreatedAt
                Kind = DM <| normalize dm.Text
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

    let logger = LogManager.GetLogger "Twitter"
    let logInfof fmt = logInfof logger fmt

    // Twitter uses Unix time; let's convert to DateTime    
    let unixEpoch = DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc)
    let fromUnix (unixTime : int) =
        unixTime
        |> float
        |> TimeSpan.FromSeconds
        |> unixEpoch.Add
    
    let appSettings = ConfigurationManager.AppSettings

    let apiKey            = appSettings.["apiKey"]
    let apiSecret         = appSettings.["apiSecret"]
    let accessToken       = appSettings.["accessToken"]
    let accessTokenSecret = appSettings.["accessTokenSecret"]

    let context = 
        let credentials = SingleUserInMemoryCredentialStore()
        credentials.ConsumerKey       <- apiKey
        credentials.ConsumerSecret    <- apiSecret
        credentials.AccessToken       <- accessToken
        credentials.AccessTokenSecret <- accessTokenSecret
        
        let authorizer = SingleUserAuthorizer()
        authorizer.CredentialStore    <- credentials

        new TwitterContext(authorizer)

    let imgurClientId     = appSettings.["imgurClientId"]
    let imgurClientSecret = appSettings.["imgurClientSecret"]
    let imgurClient   = new ImgurClient(imgurClientId, imgurClientSecret);
    let imgurEndpoint = new ImageEndpoint(imgurClient);

    let uploadImageToImgur (image : Bitmap) = async {
        use stream = new MemoryStream()
        image.Save(stream, Imaging.ImageFormat.Png)
        stream.Position <- 0L
        return! imgurEndpoint.UploadImageStreamAsync(stream)
                |> Async.AwaitTask
    }

    (* Handling Twitter rate limits: 
       context.RateLimit... gives us information on rate limits that apply to 
       the previous call we made. 
       If we have some rate left, just wait for the average time allowed between 
       calls, otherwise, wait until the next rate limit reset time.
    *)

    // small utility to check how rates work
    let prettyTime (unixTime : int) = 
        (unixTime |> fromUnix).ToShortTimeString()

    let logCurrentLimits () =
        logInfof "Current limit:"
        logInfof 
            "Rate:  total:%i remaining:%i reset:%s" 
            context.RateLimitCurrent
            context.RateLimitRemaining
            (context.RateLimitReset |> prettyTime)
        logInfof 
            "Media: total:%i remaining:%i reset:%s" 
            context.MediaRateLimitCurrent 
            context.MediaRateLimitRemaining 
            (context.MediaRateLimitReset |> prettyTime)
         
    // 15 minutes is the Twitter reference, ex
    // 180 calls = 180/15 minutes window
    let callWindow   = 15. 
    let safetyBuffer = 5. |> TimeSpan.FromSeconds

    // how long until the rate limit is reset
    let resetDelay (context : TwitterContext) =
        let nextReset = 
            context.RateLimitReset
            |> fromUnix
        (nextReset + safetyBuffer) - DateTime.UtcNow
        
    let delayUntilNextCall (context : TwitterContext) =
        let delay =
            if context.RateLimitRemaining > 0
            then
                callWindow / (float context.RateLimitCurrent)
                |> TimeSpan.FromMinutes
            else
                let nextReset = 
                    context.RateLimitReset
                    |> float
                    |> TimeSpan.FromSeconds
                    |> unixEpoch.Add
                nextReset - DateTime.UtcNow
        delay.Add safetyBuffer

    let pullDMs (sinceId : Id option) =
        let messages = 
            match sinceId with
            | None ->
                query { 
                    for dm in context.DirectMessage do 
                    where (dm.Type = DirectMessageType.SentTo)
                    select dm 
                }
            | Some(id) ->
                query { 
                    for dm in context.DirectMessage do 
                    where (dm.Type = DirectMessageType.SentTo && dm.SinceID = id)
                    where (dm.IDResponse <> id)
                    select dm
                }
            |> Seq.toList

        let wait = delayUntilNextCall context
        logInfof "pullMentions : next call in %A" wait

        let nextId =
            match messages with
            | []  -> sinceId
            | lst -> lst |> Seq.map (fun s -> s.IDResponse) |> Seq.max |> Some

        messages |> List.map Interaction.Of, nextId, wait

    let pullMentions (sinceId : Id Option) =
        let mentions = 
            match sinceId with
            | None ->
                query { 
                    for tweet in context.Status do 
                    where (tweet.Type = StatusType.Mentions)
                    select tweet 
                }
            | Some(id) ->
                query { 
                    for tweet in context.Status do 
                    where (tweet.Type = StatusType.Mentions && tweet.SinceID = id)
                    where (tweet.StatusID <> id)
                    select tweet
                }
            |> Seq.toList

        let wait = delayUntilNextCall context
        logInfof "pullMentions : next call in %A" wait

        let nextId =
            match mentions with
            | []  -> sinceId
            | lst -> lst |> Seq.map (fun s -> s.StatusID) |> Seq.max |> Some

        mentions |> List.map Interaction.Of, nextId, wait

    let trimToTweet (mediaIds : Id list) (msg : string) =
        let maxLen =
            match mediaIds with
            | [] -> 140
            | _  -> 116

        if msg.Length > maxLen 
        then msg.Substring(0, maxLen-6) + " [...]"
        else msg

    type Agent<'T> = MailboxProcessor<'T>

    type TwitterAction =
        | SendReply   of Response
        | Tweet       of Tweet
        | UploadImage of Bitmap * AsyncReplyChannel<Id>

    let twitterAgent = Agent<TwitterAction>.StartProtected(fun inbox ->
        let reply (resp : Response) = async {
            match resp.Kind with
            | ResponseKind.Tweet -> 
                let mediaIds = resp.Media |> List.map snd
                let message = 
                    sprintf ".%s %s" resp.SenderHandle resp.Reply
                    |> trimToTweet mediaIds
                do! context.ReplyAsync(resp.SenderMessageId, message, mediaIds)
                    |> Async.AwaitTask
                    |> Async.Ignore
            | ResponseKind.DM -> 
                let! uploadedImgs =
                    resp.Media 
                    |> List.map (fun (bitmap, _) -> uploadImageToImgur bitmap)
                    |> Async.Parallel
                let imgLinks = uploadedImgs |> Array.map (fun img -> img.Link)
                let message =
                    sprintf 
                        "You queried:\n%s\nRendered image(s):%s" 
                        resp.SenderMessage
                        (String.Join("\n", imgLinks))
                do! context.NewDirectMessageAsync(resp.SenderHandle, message)
                    |> Async.AwaitTask
                    |> Async.Ignore
        }

        let tweet (tweet : Tweet) = async {
            let msg = trimToTweet tweet.MediaIds tweet.Message
            do! context.TweetAsync(msg, tweet.MediaIds)
                |> Async.AwaitTask
                |> Async.Ignore
        }

        let upload (bitmap : Bitmap) = async {
            use stream = new MemoryStream()
            bitmap.Save(stream, Imaging.ImageFormat.Png)
          
            let! media = 
                stream.ToArray ()
                |> context.UploadMediaAsync
                |> Async.AwaitTask

            logInfof "Media uploaded with ID %i" media.MediaID

            logCurrentLimits ()

            return media.MediaID
        }

        let rec loop () = async {
            let! action = inbox.Receive ()

            match action with
            | SendReply resp ->
                logInfof "Posting reply to [%s] : %s" resp.SenderHandle resp.Reply
                do! reply resp
            | Tweet t ->
                logInfof "Sending new tweet : %s" t.Message
                do! tweet t
            | UploadImage (bitmap, replyChannel) ->
                let! mediaId = upload bitmap
                replyChannel.Reply mediaId

            logCurrentLimits ()

            if (context.RateLimitRemaining = 0)
            then
                logInfof "Rate limit reached, waiting for limit reset..."
                let wait = resetDelay context
                let ms   = wait.TotalMilliseconds |> int
                do! Async.Sleep ms
            return! loop () 
        }

        loop ())

    let uploadImage bitmap = async {
        let! mediaId = 
            twitterAgent.PostAndAsyncReply (fun reply -> UploadImage(bitmap, reply))
        return mediaId
    }

    let send response = async {
        twitterAgent.Post (SendReply response)   
    }

    let tweet t = async {
        twitterAgent.Post (Tweet t)
    }

    let follow (userId : uint64) = async {
        if userId <> 0UL then
            let! user = context.CreateFriendshipAsync(userId, true) |> Async.AwaitTask
            logInfof "Followed user %s" <| user.PrettyPrint()
    }