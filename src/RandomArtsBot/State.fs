namespace RandomArtsBot

open System
open System.IO

open Amazon.DynamoDBv2
open Amazon.DynamoDBv2.Model
open System.Collections.Generic

type Speaker = 
    | Us | Them

    static member Parse = function
        | "us"   -> Us
        | "them" -> Them

    override x.ToString() =
        match x with
        | Us   -> "us"
        | Them -> "them"

type IState =
    /// returns the converstions with this recipient so far
    abstract member GetConvo : string -> Async<seq<DateTime * Speaker * string>>

    /// add lines to an ongoing conversation with a recipient
    abstract member AddConvo : string * seq<DateTime * Speaker * string> -> Async<unit>

    /// returns the ID of the last DM that had been processed
    abstract member LastMessage : string -> Async<Id option>

    /// updates the ID of the last DM that had been processed
    abstract member UpdateLastMessage : string * Id -> Async<unit>

    /// returns the ID of the last mention that had been processed
    abstract member LastMention : string -> Async<Id option>

    /// updates the ID of the last mention that had been processed
    abstract member UpdateLastMention : string * Id -> Async<unit>

    /// atomically save an expr
    abstract member AtomicSave : Expr -> Async<bool>

[<AutoOpen>]
module DynamoDBUtils =
    let dynamoDB   = new AmazonDynamoDBClient()
    let stateTable = "RandomArtsBot.State"
    let exprsTable = "RandomArtsBot.PublishedExprs"

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

type State () =
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

    let getConvo recipientName = async {
        let path = Path.Combine(convoFolder, recipientName)
        if not <| File.Exists path 
        then return Seq.empty<DateTime * Speaker * string>
        else
            let convo =
                File.ReadAllLines path
                |> Array.map (fun line ->
                    let [| dt; s; msg |] = line.Split([|','|], 3)
                    let timestamp = DateTime.ParseExact(dt, datetimeFormat, null)
                    timestamp, Speaker.Parse s, msg)
                |> Seq.sortByDescending (fun (dt, _, _) -> dt)
            return convo
    }

    let addConvo recipientName (convo : seq<DateTime * Speaker * string>) = async {
        let path = Path.Combine(convoFolder, recipientName)
        let lines = 
            convo 
            |> Seq.map (fun (dt, s, msg) -> 
                sprintf "%s,%O,%s" (dt.ToString(datetimeFormat)) s msg)
        File.AppendAllLines(path, lines)
    }

    let lastMention (botname : string) = async {
        let keys = Dictionary<string, AttributeValue>()
        keys.["BotName"] <- new AttributeValue(botname)
        let! res = dynamoDB.GetItemAsync(stateTable, keys, true) |> Async.AwaitTask
        match res.Item.TryGetValue "LastMention" with
        | true, x -> return Some (uint64 <| x.S)
        | _       -> return None
    }

    let lastMessage (botname : string) = async {
        let keys = Dictionary<string, AttributeValue>()
        keys.["BotName"] <- new AttributeValue(botname)
        let! res = dynamoDB.GetItemAsync(stateTable, keys, true) |> Async.AwaitTask
        match res.Item.TryGetValue "LastMessage" with
        | true, x -> return Some (uint64 <| x.S)
        | _       -> return None
    }
    
    let updateLastMention (botname : string) (statusId : Id) = async {
        let req = UpdateItemRequest(TableName = stateTable)
        req.Key.Add("BotName", new AttributeValue(botname))
        req.AttributeUpdates.Add(
            "LastMention", 
            new AttributeValueUpdate(
                new AttributeValue(string statusId),
                AttributeAction.PUT))
        do! dynamoDB.UpdateItemAsync req |> Async.AwaitTask |> Async.Ignore
    }
    
    let updateLastMessage (botname : string) (msgId : Id) = async {
        let req = UpdateItemRequest(TableName = stateTable)
        req.Key.Add("BotName", new AttributeValue(botname))
        req.AttributeUpdates.Add(
            "LastMessage", 
            new AttributeValueUpdate(
                new AttributeValue(string msgId),
                AttributeAction.PUT))
        do! dynamoDB.UpdateItemAsync req |> Async.AwaitTask |> Async.Ignore
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
    
    do DynamoDBUtils.init ()

    interface IState with
        member __.GetConvo sender = getConvo sender
        member __.AddConvo (sender, convo) = addConvo sender convo
        member __.LastMessage botname = lastMessage botname
        member __.UpdateLastMessage (botname, id) = updateLastMessage botname id
        member __.LastMention botname = lastMention botname
        member __.UpdateLastMention (botname, id) = updateLastMention botname id
        member __.AtomicSave expr = atomicSave expr