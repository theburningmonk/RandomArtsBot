namespace RandomArtsBot

open System
open log4net
open Extensions

type Bot (botname : string) =
    let logger = LogManager.GetLogger botname

    let twitterClient = new TwitterClient()
    let artist = new Artist()
    let state  = new State()
    
    let responder = 
        new Responder(twitterClient, artist, state) :> IResponder
    let generator = 
        new Generator(twitterClient, artist, state) :> IGenerator

    member __.Start () =
        logInfof logger "[%s] starting..." botname

        responder.Start botname
        generator.Start 600000

        logInfof logger "[%s] started" botname

    member __.Stop () =
        logInfof logger "[%s] stopped" botname