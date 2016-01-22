namespace RandomArtsBot

open NLog

module Processor = 
    open System
    open System.Text.RegularExpressions
    open LinqToTwitter
    open Twitter
    open RandomArt

    let logger = LogManager.GetLogger "Processor"
    let logInfof fmt = logInfof logger fmt

    let normalize botname (text : string) = 
        text.ToLower().Replace(botname, "").Trim()

    let probablyQuery (text : string) =
        (text.Contains "(" 
        || text.Contains "x"
        || text.Contains "y"
        || text.Contains "const")

    let (|Query|InvalidQuery|Help|Mention|) text =
        if probablyQuery text then 
            match parse text with
            | Choice1Of2 expr -> Query expr
            | Choice2Of2 err  -> InvalidQuery err
        elif text = "help" then Help
        else Mention

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
            return createResp 
                    "Thank you for your interest, plz see doc : http://theburningmonk.github.io/RandomArtsBot" 
                    []
        | Mention ->
            return createResp "Thank you for your attention :-)" []
        | Query expr -> 
            let random  = new Random(int DateTime.UtcNow.Ticks)
            let path, _ = RandomArt.drawImage random expr
            let! mediaId = Twitter.uploadImage path
            return createResp "here you go" [ mediaId ]
        | InvalidQuery _ ->
            return createResp 
                    "I didn't understand that :-( plz see doc : http://theburningmonk.github.io/RandomArtsBot" 
                    []
    }

    let createTweet () = async {
        let rec attempt n = async {
            let random = new Random(int System.DateTime.UtcNow.Ticks)
            if n = 0 then return None
            else 
                let expr = RandomArt.genExpr random 5
                let! isNewExpr = State.atomicSave expr
                if not isNewExpr then
                    return! attempt (n-1)
                else 
                    let path, bitmap = RandomArt.drawImage random expr
                    if Critic.isGoodEnough bitmap then
                        let! mediaId = Twitter.uploadImage path
                        let tweet = 
                            {
                                Message  = expr.ToString()
                                MediaIDs = [ mediaId  ]
                            }
                        return Some tweet
                    else
                        return! attempt (n-1)
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
            let! response = createResponse botname mention
            do! Twitter.send response

        if List.isEmpty mentions then
            let! tweet = createTweet ()
            match tweet with
            | Some x -> 
                do! Twitter.tweet x
                printfn "generated new tweet %s" x.Message
            | _      -> ()

        do! Async.Sleep (int delay.TotalMilliseconds)

        return! loop botname nextId
    }

type Bot (botname) =
    let logger = LogManager.GetLogger botname

    member __.Start () =
        logInfof logger "[%s] starting..." botname

        let lastMention = State.lastMention botname |> Async.RunSynchronously
        Processor.loop botname lastMention |> Async.Start

        logInfof logger "[%s] started" botname

    member __.Stop () =
        logInfof logger "[%s] stopped" botname
