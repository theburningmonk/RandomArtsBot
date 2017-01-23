#r "System.Net.Http"
#r "FSharp.Data"
#r "Newtonsoft.Json"

open System.Net
open System.Net.Http
open FSharp.Data
open Newtonsoft.Json

let Run(req: HttpRequestMessage, log: TraceWriter) =
  log.Info(sprintf "F# HTTP trigger function processed a request. ")
    
  let response = new HttpResponseMessage()
  response.Content <- new StringContent("fuck you")
  response.StatusCode <- HttpStatusCode.OK
  
  response