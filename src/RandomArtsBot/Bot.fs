namespace RandomArtsBot

open log4net

module Processor = 
    open System
    open System.Text.RegularExpressions
    open LinqToTwitter
    open Twitter
    open RandomArt
    open State
    open Extensions

    let logger = LogManager.GetLogger "Processor"
    let logInfof fmt = logInfof logger fmt

    let normalize botname (text : string) = 
        text.ToLower().Replace(botname, "").Trim()

    let probablyQuery (text : string) =
        if text.Contains "(" then true
        else
            match text.Trim().ToLower() with
            | "x" | "y" | "const" -> true
            | _ -> false

    let (|Query|InvalidQuery|Help|Mention|) text =
        if probablyQuery text then 
            match parse text with
            | Choice1Of2 expr -> Query expr
            | Choice2Of2 err  -> InvalidQuery err
        elif text = "help" then Help
        else Mention

    let sameAsLastTime text (convo : seq<DateTime * Speaker * string>) =
        let theirMessages =
            convo 
            |> Seq.choose (function
                | _, Them, msg -> Some msg
                | _            -> None)

        if Seq.isEmpty theirMessages 
        then false
        else theirMessages |> Seq.head |> ((=) text)

    let invalidQueryStreak n (convo : seq<DateTime * Speaker * string>) =
        let theirMessages =
            convo 
            |> Seq.choose (function
                | dt, Them, msg -> Some (dt, msg)
                | _             -> None)

        let now = DateTime.UtcNow
        let msgs = // messages from them in the last hour
            theirMessages
            |> Seq.take n
            |> Seq.takeWhile (fun (dt, _) -> 
                (now - dt) < TimeSpan.FromHours 1.0)
            |> Seq.map snd

        if Seq.length msgs < n then false
        else 
            msgs 
            |> Seq.forall (function 
                | InvalidQuery _
                | Mention        -> true
                | _              -> false)

    /// Determines if we should keep conversing with the sender.
    /// Don't respond, if:
    ///     - sender sent the same invalid query twice in a row
    ///     - sender already sent 5 invalid queries in a row
    let shouldKeepTalking sender invalidQuery = async {
        let! convo = State.getConvo sender
        
        return sameAsLastTime invalidQuery convo
            || invalidQueryStreak 5 convo
    }

    let createResponse botname (status : Status) = async {
        let recipient = status.User.ScreenNameResponse
        let statusId  = status.StatusID
        let text      = normalize botname status.Text

        logInfof "Responding to [%s] [%d] [%s]" recipient statusId text

        let createResp msg mediaIds =
            {
                RecipientName = recipient
                StatusID      = statusId
                Message       = msg
                MediaIDs      = mediaIds
            }

        match text with
        | Help -> 
            let msg = "Thank you for your interest, plz see doc : http://theburningmonk.github.io/RandomArtsBot"
            return Some <| createResp msg []
        | Mention ->
            return Some <| createResp "Thank you for your attention :-)" []
        | Query expr -> 
            let random  = new Random(int DateTime.UtcNow.Ticks)
            let path, _ = RandomArt.drawImage random expr
            let! mediaId = Twitter.uploadImage path
            return Some <| createResp "here you go" [ mediaId ]
        | InvalidQuery err ->
            let! keepTalking = shouldKeepTalking recipient text
            if not <| keepTalking then
                logInfof "mm.. suspicious conversation, let's stop talking"
                return None
            else
                let msg = "I didn't understand that :-( plz see doc : http://theburningmonk.github.io/RandomArtsBot" 
                return Some <| createResp msg []
    }

    let (|GoodEnough|_|) random expr =
        let path, bitmap = RandomArt.drawImage random expr
        if Critic.isGoodEnough bitmap 
        then Some path
        else None

    let createTweet () = async {
        let rec attempt n = async {
            let random = new Random(int System.DateTime.UtcNow.Ticks)

            if n = 0 then return None
            else
                let expr = RandomArt.genExpr random 6
                logInfof "Generated expression :\n\t%O\n" expr
                let! isNewExpr = State.atomicSave expr
                match isNewExpr, expr with
                | true, GoodEnough random path ->
                    let! mediaId = Twitter.uploadImage path
                    let tweet = 
                        {
                            Message  = expr.ToString()
                            MediaIDs = [ mediaId  ]
                        }
                    return Some tweet
                | _ -> return! attempt (n-1)
        }
        
        return! attempt 10
    }
        
    let rec loop botname sinceId = async {
        logInfof "[%s] Checking for new mentions" botname
        let mentions, nextId, delay = Twitter.pullMentions sinceId

        match nextId with
        | Some id -> do! State.updateLastMention botname id
        | _ -> ()
      
        for mention in mentions do
            logInfof "Mentioned by %s" <| mention.User.PrettyPrint()
            logInfof "Message : %s" mention.Text

            let! response = createResponse botname mention
            match response with
            | Some x -> do! Twitter.send x
            | _ -> ()

            do! Twitter.follow (uint64 mention.User.UserIDResponse)

        if List.isEmpty mentions then
            let! tweet = createTweet ()
            match tweet with
            | Some x -> 
                do! Twitter.tweet x
                logInfof "Published new tweet :\n\t%s\n" x.Message
            | _      -> ()

        do! Async.Sleep (int delay.TotalMilliseconds)

        return! loop botname nextId
    }

type Bot (botname : string) =
    let logger = LogManager.GetLogger botname

    member __.Start () =
        logInfof logger "[%s] starting..." botname

        let lastMention = State.lastMention botname |> Async.RunSynchronously
        Processor.loop botname lastMention |> Async.Start

        logInfof logger "[%s] started" botname

    member __.Stop () =
        logInfof logger "[%s] stopped" botname