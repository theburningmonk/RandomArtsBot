namespace RandomArtsBot

module Twitter =
    open System
    open System.Configuration
    open System.Drawing
    open System.IO

    open LinqToTwitter
    open NLog

    type SinceID  = uint64
    type StatusID = uint64
    type MediaID  = uint64

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

    let pullMentions (sinceID : SinceID Option) =
        let mentions = 
            match sinceID with
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

        let updatedSinceID =
            match mentions with
            | []    -> sinceID
            | hd::_ -> Some hd.StatusID
                                                                  
        mentions, updatedSinceID, wait

    let trimToTweet (msg : string) =
        if msg.Length > 140 
        then msg.Substring(0,134) + " [...]"
        else msg

    type Response = 
        {
            RecipientName : string
            StatusID      : StatusID
            Message       : string
            MediaIDs      : MediaID list
        }

    type Agent<'T> = MailboxProcessor<'T>

    let responsesAgent = Agent<Response>.Start(fun inbox ->
        let send (resp : Response) = async {
            let message = 
                sprintf "@%s %s" resp.RecipientName resp.Message
                |> trimToTweet

            do! context.ReplyAsync(resp.StatusID, message, resp.MediaIDs) 
                |> Async.AwaitTask
                |> Async.Ignore
        }

        let rec loop () = async {
            let! response = inbox.Receive ()

            logInfof "Posting response: %s" response.Message
            do! send response

            logCurrentLimits ()

            if (context.RateLimitRemaining = 0)
            then
                logInfof "Send reply: waiting for limit reset"
                let wait = resetDelay context
                let ms   = wait.TotalMilliseconds |> int
                do! Async.Sleep ms
            return! loop () 
        }

        loop ())

    let mediaUploadAgent = Agent<string * AsyncReplyChannel<uint64>>.Start(fun inbox ->
        let upload (path : string) = async {
            logInfof "Uploading image from %s" path

            use img    = Image.FromFile(path)
            use stream = new MemoryStream()
            img.Save(stream, Imaging.ImageFormat.Png)
          
            let! media = 
                stream.ToArray ()
                |> context.UploadMediaAsync
                |> Async.AwaitTask

            logInfof "Media uploaded with ID %i" media.MediaID

            logCurrentLimits ()

            img.Dispose ()
            File.Delete path

            logInfof "Deleted file from %s" path

            return media.MediaID
        }

        let rec loop () = async {
            let! (path, replyChannel) = inbox.Receive ()

            logInfof "Uploading image [%s]..." path
            let! mediaID = upload path
            replyChannel.Reply mediaID
            logInfof "Uploaded image [%s]" path

            logCurrentLimits ()

            if (context.RateLimitRemaining = 0)
            then
                logInfof "Media upload: waiting for limit reset"
                let wait = resetDelay context
                let ms = wait.TotalMilliseconds |> int
                do! Async.Sleep ms
            return! loop () 
        }

        loop ())

    let uploadImage path = async {
        let! mediaId = 
            mediaUploadAgent.PostAndAsyncReply (fun reply -> path, reply)
        return mediaId
    }

    let send response = async {
        responsesAgent.Post response    
    }