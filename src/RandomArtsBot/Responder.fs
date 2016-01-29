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

    let probablyQuery (text : string) =
        if text.Contains "(" then true
        else
            match text.Trim().ToLower() with
            | "x" | "y" | "const" -> true
            | _ -> false

    let (|Query|InvalidQuery|Help|Other|) text =
        if probablyQuery text then 
            match parse text with
            | Choice1Of2 expr -> Query expr
            | Choice2Of2 err  -> InvalidQuery err
        elif text = "help" then Help
        else Other text

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
                | Other _ -> true
                | _       -> false)
    
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

    let getMentions botname sinceId = async {
        logInfof "[%s] Checking for new mentions..." botname
        let mentions, nextId, delay = Twitter.pullMentions sinceId

        match mentions with
        | [] -> logInfof "[%s] No new mentions." botname
        | _  -> logInfof "[%s] Found %d new mentions." botname mentions.Length

        return mentions, nextId, delay
    }

    let getMessages botname sinceId = async {
        logInfof "[%s] Checking for new DMs..." botname
        let mentions, nextId, delay = Twitter.pullDMs sinceId

        match mentions with
        | [] -> logInfof "[%s] No new DMs." botname
        | _  -> logInfof "[%s] Found %d new DMs." botname mentions.Length

        return mentions, nextId, delay
    }

    let createResponse (interaction : Interaction) = async {
        logInfof "Responding to [%O]" interaction

        let responseKind = 
            match interaction.Kind with
            | InteractionKind.DM _ -> ResponseKind.DM
            | _                    -> ResponseKind.Tweet

        let createResp msg reply media =
            {
                SenderHandle    = interaction.User.Handle
                SenderMessage   = msg
                SenderMessageId = interaction.Id
                Kind            = responseKind
                Reply           = reply
                Media           = media
            }

        match interaction.Kind with
        | Retweet | Fav -> 
            return None
        | Mention (Help as msg) | InteractionKind.DM (Help as msg) -> 
            let reply = "Thank you for your interest, plz see doc : http://theburningmonk.github.io/RandomArtsBot"
            return Some <| createResp msg reply []
        | Mention (Other msg) | InteractionKind.DM (Other msg) -> 
            let! keepTalking = shouldKeepTalking interaction.User.Handle msg
            if not keepTalking then
                logInfof "mm.. suspicious conversation, let's stop talking"
                return None
            else
                let reply = "Thank you for your attention :-)"
                return Some <| createResp msg reply []
        | Mention (Query expr as msg) | InteractionKind.DM (Query expr as msg) -> 
            let random   = new Random(int DateTime.UtcNow.Ticks)
            let bitmap   = RandomArt.drawImage random expr
            let! mediaId = Twitter.uploadImage bitmap
            let reply    = "here you go"
            return Some <| createResp msg reply [ bitmap, mediaId ]
        | Mention (InvalidQuery err as msg) 
        | InteractionKind.DM (InvalidQuery err as msg) -> 
            let! keepTalking = shouldKeepTalking interaction.User.Handle msg
            if not keepTalking then
                logInfof "mm.. suspicious conversation, let's stop talking"
                return None
            else
                let reply = "I didn't understand that :-( plz see doc : http://theburningmonk.github.io/RandomArtsBot" 
                return Some <| createResp msg reply []
    }

    let rec loop 
            (getInteractions : Id option -> Async<Interaction list * Id option * TimeSpan>) 
            (updateLast : Id -> Async<unit>)
            (sinceId : Id option) = async {
        let! interactions, nextId, delay = getInteractions sinceId

        match nextId with
        | Some id -> do! updateLast id
        | _ -> ()
      
        for interaction in interactions do
            let! response = createResponse interaction
            match response with
            | Some x -> 
                do! Twitter.send x
                do! State.addConvo 
                        interaction.User.Handle 
                        [ interaction.CreatedAt, Them, x.SenderMessage
                          DateTime.UtcNow, Us, x.Reply ]
            | _ ->
                match interaction.Kind with
                | Mention msg
                | InteractionKind.DM msg ->
                    do! State.addConvo
                            interaction.User.Handle
                            [ interaction.CreatedAt, Them, msg ]
                | _ -> ()

            do! Twitter.follow interaction.User.Id

        do! Async.Sleep (int delay.TotalMilliseconds)

        return! loop getInteractions updateLast nextId
    }

    let start botname = 
        let lastMention = State.lastMention botname |> Async.RunSynchronously
        match lastMention with
        | Some id -> logInfof "Last mention : %d" id
        | _       -> logInfof "NO last mention found"

        let getMentions = getMentions botname
        let updateLastMention = State.updateLastMention botname

        Async.StartProtected(
            loop getMentions updateLastMention lastMention,
            fun exn -> logErrorf "%A" exn)

        logInfof "Started loop to poll and respond to new mentions"

        let lastMessage = State.lastMessage botname |> Async.RunSynchronously
        match lastMessage with
        | Some id -> logInfof "Last message : %d" id
        | _       -> logInfof "NO last message found"
        
        let getMessages = getMessages botname
        let updateLastMessage = State.updateLastMessage botname

        Async.StartProtected(
            loop getMessages updateLastMessage lastMessage,
            fun exn -> logErrorf "%A" exn)

        logInfof "Started loop to poll and respond to new DMs"