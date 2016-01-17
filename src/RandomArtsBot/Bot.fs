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
                    "Thank you for your interest, plz syntax reference here" 
                    []
        | Mention ->
            return createResp "Thank you for your attention :-)" []
        | Query expr -> 
            let path = RandomArt.drawImage expr
            let! mediaId = Twitter.uploadImage path
            return createResp "here you go" [ mediaId ]
        | InvalidQuery err ->
            return createResp err []
    }
        
    let rec loop botname sinceId = async {
        logInfof "[%s] Checking for new mentions" botname
        let mentions, nextID, delay = Twitter.pullMentions sinceId

//        nextID 
//        |> Option.iter (Storage.updateLastMentionID)
      
        for mention in mentions do
            let! response = createResponse botname mention
            do! Twitter.send response

        do! Async.Sleep (int delay.TotalMilliseconds)

        return! loop botname nextID
    }

type Bot (botname) =
    let logger = LogManager.GetLogger botname

    member this.Start () =
        logInfof logger "[%s] starting..." botname

        //Storage.readLastMentionID ()
        Processor.loop botname None
        |> Async.Start

        logInfof logger "[%s] started" botname

    member this.Stop () =
        logInfof logger "[%s] stopped" botname
