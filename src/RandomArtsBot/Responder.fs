namespace RandomArtsBot

open System
open log4net
open RandomArt
open State
open Twitter

module Responder =
    let logger = LogManager.GetLogger "Processor"
    let logInfof fmt  = logInfof logger fmt
    let logErrorf fmt = logErrorf logger fmt

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

    let sameAsLastTime text timeFrame (convo : seq<DateTime * Speaker * string>) =
        let now = DateTime.UtcNow
        let theirMessages =
            convo 
            |> Seq.filter (function
                | dt, _, _ -> now - dt < timeFrame)
            |> Seq.choose (function
                | _, Them, msg -> Some msg
                | _            -> None)

        if Seq.isEmpty theirMessages 
        then false
        else theirMessages |> Seq.head |> ((=) text)

    let invalidQueryStreak n timeFrame (convo : seq<DateTime * Speaker * string>) =
        let theirMessages =
            convo 
            |> Seq.choose (function
                | dt, Them, msg -> Some (dt, msg)
                | _             -> None)

        let now = DateTime.UtcNow
        let msgs =
            theirMessages
            |> Seq.take n
            |> Seq.takeWhile (fun (dt, _) -> (now - dt) < timeFrame)
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
    let shouldKeepTalking sender text = async {
        let! convo = State.getConvo sender
        
        let ``3 mins``  = TimeSpan.FromMinutes 3.0
        let ``10 mins`` = TimeSpan.FromMinutes 10.0

        return (not <| sameAsLastTime text ``3 mins`` convo)
            && (not <| invalidQueryStreak 5 ``10 mins`` convo)
    }

    type Mention =
        {
            Id        : StatusID 
            Sender    : string
            SenderId  : uint64
            Message   : string
            Timestamp : DateTime
        }

    let getMentions botname sinceId = async {
        logInfof "[%s] Checking for new mentions..." botname
        let statuses, nextId, delay = Twitter.pullMentions sinceId

        match statuses with
        | [] -> logInfof "[%s] No new mentions." botname
        | _  -> logInfof "[%s] Found %d new mentions." botname statuses.Length

        let mentions =
            statuses 
            |> List.map (fun s ->
                {
                    Id        = s.StatusID
                    Sender    = s.User.ScreenNameResponse
                    SenderId  = uint64 s.User.UserIDResponse
                    Message   = normalize botname s.Text
                    Timestamp = s.CreatedAt
                })

        return mentions, nextId, delay
    }

    let createResponse { Id = id; Sender = sender; Message = msg } = async {
        logInfof "Responding to [%s] [%d] [%s]" sender id msg

        let createResp msg mediaIds =
            {
                RecipientName = sender
                StatusID      = id
                Message       = msg
                MediaIDs      = mediaIds
            }

        match msg with
        | Help -> 
            let msg = "Thank you for your interest, plz see doc : http://theburningmonk.github.io/RandomArtsBot"
            return Some <| createResp msg []
        | Mention ->
            let! keepTalking = shouldKeepTalking sender msg
            if not keepTalking then
                logInfof "mm.. suspicious conversation, let's stop talking"
                return None
            else
                return Some <| createResp "Thank you for your attention :-)" []
        | Query expr -> 
            let random  = new Random(int DateTime.UtcNow.Ticks)
            let path, _ = RandomArt.drawImage random expr
            let! mediaId = Twitter.uploadImage path
            return Some <| createResp "here you go" [ mediaId ]
        | InvalidQuery err ->
            let! keepTalking = shouldKeepTalking sender msg
            if not keepTalking then
                logInfof "mm.. suspicious conversation, let's stop talking"
                return None
            else
                let msg = "I didn't understand that :-( plz see doc : http://theburningmonk.github.io/RandomArtsBot" 
                return Some <| createResp msg []
    }

    let rec loop botname sinceId = async {
        let! mentions, nextId, delay = getMentions botname sinceId

        match nextId with
        | Some id -> do! State.updateLastMention botname id
        | _ -> ()
      
        for mention in mentions do
            let! response = createResponse mention
            match response with
            | Some x -> 
                do! Twitter.send x
                do! State.addConvo 
                        mention.Sender 
                        [ mention.Timestamp, Them, mention.Message
                          DateTime.UtcNow, Us, x.Message ]
            | _ ->
                do! State.addConvo
                        mention.Sender
                        [ mention.Timestamp, Them, mention.Message ]

            do! Twitter.follow mention.SenderId

        do! Async.Sleep (int delay.TotalMilliseconds)

        return! loop botname nextId
    }

    let start botname = 
        let lastMention = State.lastMention botname |> Async.RunSynchronously
        match lastMention with
        | Some id -> logInfof "Last mention : %d" id
        | _       -> logInfof "NO last mention found"

        Async.StartProtected(
            loop botname lastMention,
            fun exn -> logErrorf "%A" exn)

        logInfof "Started loop to poll and respond to new mentions"