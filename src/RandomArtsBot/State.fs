namespace RandomArtsBot

open System
open System.IO

open RandomArt
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

    let getConvo recipientName = async {
        let path = Path.Combine(convoFolder, recipientName)
        if not <| File.Exists path 
        then return [||]
        else
            let convo =
                File.ReadAllLines path
                |> Array.map (fun line ->
                    let [| dt; s; msg |] = line.Split([|','|], 3)
                    let timestamp = DateTime.ParseExact(dt, datetimeFormat, null)
                    timestamp, Speaker.Parse s, msg)
            return convo
    }

    let addConvo recipientName (convo : seq<DateTime * Speaker * string>) = async {
        let path = Path.Combine(convoFolder, recipientName)
        let lines = 
            convo 
            |> Seq.map (fun (dt, s, msg) -> 
                sprintf "%s, %O, %s" (dt.ToString(datetimeFormat)) s msg)
        File.AppendAllLines(path, lines)
    }

    open Amazon.DynamoDBv2
    open Amazon.DynamoDBv2.Model
    open System.Collections.Generic

    let dynamoDB   = new AmazonDynamoDBClient()
    let stateTable = "RandomArtsBot.State"
    let exprsTable = "RandomArtsBot.PublishedExprs"

    [<AutoOpen>]
    module DynamoDBUtils =
        let listAllTables () =
            let rec loop lastTableName = seq {
                let req = ListTablesRequest()
                if not <| isNull lastTableName then
                    req.ExclusiveStartTableName <- lastTableName

                let res = dynamoDB.ListTables req
                yield! res.TableNames

                if not <| isNull res.LastEvaluatedTableName then
                    yield! loop res.LastEvaluatedTableName
            }

            loop null

        let init () =
            let tableNames = 
                listAllTables () 
                |> Seq.map (fun x -> x.ToLower())
                |> Set.ofSeq

            let stateTableExists = tableNames.Contains <| stateTable.ToLower()
            let exprsTableExists = tableNames.Contains <| exprsTable.ToLower()

            if not stateTableExists then
                let req = CreateTableRequest(TableName = stateTable)
                req.KeySchema.Add(new KeySchemaElement("BotName", KeyType.HASH))
                req.ProvisionedThroughput <- new ProvisionedThroughput(1L, 1L)
                req.AttributeDefinitions.Add(
                    new AttributeDefinition("BotName", ScalarAttributeType.S))

                dynamoDB.CreateTable req |> ignore

            if not exprsTableExists then
                let req = CreateTableRequest(TableName = exprsTable)
                req.KeySchema.Add(new KeySchemaElement("Expr", KeyType.HASH))
                req.ProvisionedThroughput <- new ProvisionedThroughput(1L, 1L)
                req.AttributeDefinitions.Add(
                    new AttributeDefinition("Expr", ScalarAttributeType.S))

                dynamoDB.CreateTable req |> ignore
    
    do DynamoDBUtils.init ()

    let lastMention (botname : string) = async {
        let keys = Dictionary<string, AttributeValue>()
        keys.["BotName"] <- new AttributeValue(botname)
        let! res = dynamoDB.GetItemAsync(stateTable, keys, true) |> Async.AwaitTask
        match res.Item.TryGetValue "LastMention" with
        | true, x -> return Some (uint64 <| x.S)
        | _       -> return None
    }
    
    let updateLastMention (botname : string) (statusId : StatusID) = async {
        let req = PutItemRequest(TableName = stateTable)
        req.Item.Add("BotName", new AttributeValue(botname))
        req.Item.Add("LastMention", new AttributeValue(string statusId))
        do! dynamoDB.PutItemAsync req |> Async.AwaitTask |> Async.Ignore
    }

    let atomicSave (expr : Expr) = async {
        let key = expr.ToString()
        let timestamp = DateTime.UtcNow.ToString("yyyyMMdd HH:mm:ss")
        let req = PutItemRequest(TableName = exprsTable)
        req.Item.Add("Expr", new AttributeValue(key))
        req.Item.Add("Created", new AttributeValue(timestamp))
        req.Expected.Add("Expr", new ExpectedAttributeValue(false))
        
        let! res = 
            dynamoDB.PutItemAsync req
            |> Async.AwaitTask
            |> Async.Catch

        match res with
        | Choice1Of2 _ -> return true
        |_             -> return false
    } 