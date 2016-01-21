namespace RandomArtsBot

open System
open System.IO
open Twitter

module State =
    let datetimeFormat = "yyyyMMdd HH:mm:ss"

    let appData = 
        Environment.SpecialFolder.CommonApplicationData
        |> Environment.GetFolderPath

    let createIfNotExists dir =
        if not <| Directory.Exists dir
        then Directory.CreateDirectory dir |> ignore

    // root folder for the app
    let stateFolder = Path.Combine(appData, "randomartsbot")
    do createIfNotExists stateFolder
    
    // conversation history for each recipient
    let convoFolder = Path.Combine(stateFolder, "conversations")
    do createIfNotExists convoFolder

    type Speaker = 
        | Us | Them

        static member Parse = function
            | "us"   -> Us
            | "them" -> Them

        override x.ToString() =
            match x with
            | Us   -> "us"
            | Them -> "them"

    let getConvo recipientName =
        let path = Path.Combine(convoFolder, recipientName)
        if not <| File.Exists path then [||]
        else
            File.ReadAllLines path
            |> Array.map (fun line ->
                let [| dt; s; msg |] = line.Split([|','|], 3)
                let timestamp = DateTime.ParseExact(dt, datetimeFormat, null)
                timestamp, Speaker.Parse s, msg)

    let addConvo recipientName (convo : seq<DateTime * Speaker * string>) =
        let path = Path.Combine(convoFolder, recipientName)
        let lines = 
            convo 
            |> Seq.map (fun (dt, s, msg) -> 
                sprintf "%s, %O, %s" (dt.ToString(datetimeFormat)) s msg)
        File.AppendAllLines(path, lines)

    open Amazon.DynamoDBv2
    open Amazon.DynamoDBv2.Model
    open System.Collections.Generic

    let dynamoDB  = new AmazonDynamoDBClient()
    let tableName = "RandomArtsBot.State"

    [<AutoOpen>]
    module DynamoDBUtils =
        let listAllTables () =
            let rec loop lastTableName = seq {
                let req = ListTablesRequest()
                if not <| isNull lastTableName then
                    req.ExclusiveStartTableName <- lastTableName

                let res = dynamoDB.ListTables(req)
                yield! res.TableNames

                if not <| isNull res.LastEvaluatedTableName then
                    yield! loop res.LastEvaluatedTableName
            }

            loop null

        let init () =
            let tableNames = listAllTables () |> Seq.toArray
            let tableExists = 
                tableNames
                |> Seq.map (fun x -> x.ToLower())
                |> Seq.exists ((=) <| tableName.ToLower())

            if not tableExists then
                let req = CreateTableRequest()
                req.TableName <- tableName
                req.KeySchema.Add(new KeySchemaElement("BotName", KeyType.HASH))
                req.ProvisionedThroughput <- new ProvisionedThroughput(1L, 1L)
                req.AttributeDefinitions.Add(
                    new AttributeDefinition("BotName", ScalarAttributeType.S))

                dynamoDB.CreateTable(req) |> ignore
    
    do DynamoDBUtils.init ()

    let lastMention (botname : string) = async {
        let keys = Dictionary<string, AttributeValue>()
        keys.["BotName"] <- new AttributeValue(botname)
        let! res  = dynamoDB.GetItemAsync(tableName, keys, true) |> Async.AwaitTask
        match res.Item.TryGetValue "LastMention" with
        | true, x -> return Some (uint64 <| x.S)
        | _ -> return None
    }
    
    let updateLastMention (botname : string) (statusId : StatusID) = async {
        let req  = PutItemRequest()
        req.TableName <- tableName
        req.Item.Add("BotName", new AttributeValue(botname))
        req.Item.Add("LastMention", new AttributeValue(string statusId))
        do! dynamoDB.PutItemAsync(req) |> Async.AwaitTask |> Async.Ignore
    }