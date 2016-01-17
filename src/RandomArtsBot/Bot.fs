namespace RandomArtsBot

open NLog

module Processor = 
    open System
    open System.Text.RegularExpressions
    open LinqToTwitter

    let logger = LogManager.GetLogger "Processor"
    let logInfof fmt = logInfof logger fmt

    let normalize botname (text : string) = 
        text.ToLower().Replace(botname, "")

//    let processArguments (args:PLACE*MEASURE*TIMEFRAME) (recipient,statusID) =
//        async {
//            logger.Info "Processing arguments"
//            let result = createChart args
//            let mediaID = 
//                result.Chart 
//                |> Option.map (fun chart -> 
//                    Twitter.mediaUploadAgent.PostAndReply (fun channel -> chart, channel))
//
//            { RecipientName = recipient
//              StatusID = statusID
//              Message = result.Description
//              MediaID = mediaID }
//            |> Twitter.responsesAgent.Post }

    let respondTo (status : Status) =        
        let recipient = status.User.ScreenNameResponse
        let statusID  = status.StatusID
        let text      = status.Text

        logInfof "Responding to [%s] [%i] [%s]" recipient statusID text

//        match text with
//        | Mention -> 
//            { RecipientName = recipient
//              StatusID = statusID
//              Message = "thanks for the attention!"
//              MediaID = None }
//            |> Twitter.responsesAgent.Post
//        | Query ->
//            let arguments = 
//                text
//                |> removeBotHandle
//                |> extractArguments
//        
//            match arguments with
//            | Fail(msg) ->
//                { RecipientName = recipient
//                  StatusID = statusID
//                  Message = "failed to parse your request: " + msg
//                  MediaID = None }
//                |> Twitter.responsesAgent.Post
//            | OK(args) -> 
//                processArguments args (recipient,statusID)
//                |> Async.Start
//                |> ignore
        
    let rec loop sinceId = async {
        logInfof "Checking for new mentions"
        let mentions, nextID, delay = Twitter.pullMentions sinceId

//        nextID 
//        |> Option.iter (Storage.updateLastMentionID)
        
        mentions 
        |> List.iter respondTo

        do! Async.Sleep (int delay.TotalMilliseconds)

        return! loop (nextID) 
    }

[<AbstractClass>]
type Bot (botname) =
    let logger = LogManager.GetLogger botname

    member this.Start () =
        logInfof logger "[%s] starting..." botname

//        Storage.readLastMentionID ()
//        |> Processor.loop 
//        |> Async.Start

        logInfof logger "[%s] started" botname

    member this.Stop () =
        logInfof logger "[%s] stopped" botname