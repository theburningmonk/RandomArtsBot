namespace RandomArtsBot

[<AutoOpen>]
module Logging =
    open NLog

    let logDebugf (logger : Logger) fmt =
        Printf.ksprintf logger.Debug fmt

    let logInfof (logger : Logger) fmt = 
        Printf.ksprintf logger.Info fmt

    let logWarnf (logger : Logger) fmt = 
        Printf.ksprintf logger.Warn fmt

    let logErrorf (logger : Logger) fmt = 
        Printf.ksprintf logger.Error fmt
        
    let logFatalf (logger : Logger) fmt = 
        Printf.ksprintf logger.Fatal fmt