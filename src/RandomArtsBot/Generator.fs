namespace RandomArtsBot

open System
open log4net
open Extensions

type IGenerator =
    /// Starts a loop to generate random images periodically
    abstract member Start : freqMs:int -> unit

type Generator 
        (twitter : ITwitterClient, 
         artist  : IArtist,
         state   : IState) =
    let logger = LogManager.GetLogger(typeof<Generator>)
    let logInfof fmt  = logInfof logger fmt
    let logErrorf fmt = logErrorf logger fmt

    let (|GoodEnough|_|) random expr =
        let bitmap = artist.DrawImage(random, expr)
        if Critic.isGoodEnough bitmap 
        then Some bitmap
        else None

    let createTweet () = async {
        let rec attempt n = async {
            let random = new Random(int System.DateTime.UtcNow.Ticks)

            if n = 0 then return None
            else
                let expr = artist.GenExpr(random, 6)
                logInfof "Generated expression :\n\t%O\n" expr
                let! isNewExpr = state.AtomicSave expr
                match isNewExpr, expr with
                | true, GoodEnough random bitmap ->
                    let! mediaId = twitter.UploadImage bitmap
                    let tweet : Tweet = 
                        {
                            Message  = expr.ToString()
                            MediaIds = [ mediaId  ]
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
            do! twitter.Tweet x
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

    interface IGenerator with
        member __.Start freqMs = start freqMs