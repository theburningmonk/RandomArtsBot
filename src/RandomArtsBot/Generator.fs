namespace RandomArtsBot

open System
open log4net
open Twitter
open Extensions

module Generator =
    let logger = LogManager.GetLogger "Generator"
    let logInfof fmt  = logInfof logger fmt
    let logErrorf fmt = logErrorf logger fmt

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
                    let tweet : Tweet = 
                        {
                            Message  = expr.ToString()
                            MediaIDs = [ mediaId  ]
                        }
                    return Some tweet
                | _ -> return! attempt (n-1)
        }
        
        return! attempt 10
    }

    let rec loop freqMs = async {
        let! tweet = createTweet ()
        match tweet with
        | Some x -> 
            do! Twitter.tweet x
            logInfof "Published new tweet :\n\t%s\n" x.Message
        | _      -> ()

        do! Async.Sleep freqMs

        do! loop freqMs
    }

    let start freqMs = 
        Async.StartProtected(
            loop freqMs, 
            fun exn -> logErrorf "%A" exn)

        logInfof "started loop to generate random images every [%d] ms" freqMs