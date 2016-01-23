namespace RandomArtsBot

[<AutoOpen>]
module Logging =
    open log4net

    let logDebugf (logger : ILog) fmt =
        Printf.ksprintf logger.Debug fmt

    let logInfof (logger : ILog) fmt = 
        Printf.ksprintf logger.Info fmt

    let logWarnf (logger : ILog) fmt = 
        Printf.ksprintf logger.Warn fmt

    let logErrorf (logger : ILog) fmt = 
        Printf.ksprintf logger.Error fmt
        
    let logFatalf (logger : ILog) fmt = 
        Printf.ksprintf logger.Fatal fmt