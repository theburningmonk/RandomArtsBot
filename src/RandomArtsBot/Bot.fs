namespace RandomArtsBot

open System
open log4net
open Extensions

type Bot (botname : string) =
    let logger = LogManager.GetLogger botname

    member __.Start () =
        logInfof logger "[%s] starting..." botname

        Responder.start botname

        Generator.start 600000

        logInfof logger "[%s] started" botname

    member __.Stop () =
        logInfof logger "[%s] stopped" botname