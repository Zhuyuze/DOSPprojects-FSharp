open Suave
open Suave.Http
open Suave.Operators
open Suave.Filters
open Suave.Successful
open Suave.Files
open Suave.RequestErrors
open Suave.Logging
open Suave.Utils

open System
open System.Net

open Suave.Sockets
open Suave.Sockets.Control
open Suave.WebSocket

open System
open Akka
open Akka.FSharp
open Akka.Actor
open Akka.Configuration
open FSharp.Json

let mutable buff = ""
type Message =
    {
        Content: string
        Tag: string
        User: int
    }




type requestMessage = 
    {
        Mode: int
        UserName: string
        UserID: int
        SUserID: int
        QueryTag: string
        UserPassword: string
        Tweet: string
        Tag: string
    }


let r = System.Random()


let generateRandomString (scope:int)=
    let rv = r.Next() % scope
    let l = if rv = 0 then 1 else rv
    let mutable result = ""
    for i in 0 .. l do
        result <- result + char(r.Next() % 95 + 32).ToString()
    result


let configuration = 
    ConfigurationFactory.ParseString(
        @"akka {
            actor {
                provider = ""Akka.Remote.RemoteActorRefProvider, Akka.Remote""
                deployment {
                    /remoteecho {
                        remote = ""akka.tcp://RemoteFSharp@127.0.0.1:34567""
                    }
                }
            }
            remote {
                helios.tcp {
                    port = 0
                    hostname = ""127.0.0.1""
                }
            }
        }")
let system = ActorSystem.Create("RemoteFSharp", configuration)
let addr = "akka.tcp://RemoteFSharp@127.0.0.1:34567/user/TweeterServer"

let clientActor (mailbox: Actor<_>) =
    let mutable UserName = ""
    let mutable UserID = -1
    let mutable UserPassword = ""
    let mutable server:ActorSelection = null
    let mutable localMessage:list<Message> = []
    let rec loop() = actor {
        let! message = mailbox.Receive()
        let sender = mailbox.Sender()
        match box message with
        | :? list<Message> as l ->
            localMessage <- localMessage @ l
            printfn "Client %A: New Message Received!" UserID
        | :? int as a ->
            UserID <- a
            let request = {Mode = 5; UserName = UserName; UserID = UserID; SUserID = -1; QueryTag = ""; UserPassword = UserPassword; Tweet = ""; Tag = ""}
            let json = Json.serialize request
            server <! json
            printfn "Registered with allocated ID: %A !" a
        | :? ActorSelection as s ->
            server <- s
            printfn "Ready!"
        | :? string as s ->
            //printfn "Client %A: Start action: %A" UserID s
            if s = "reg" then
                if UserID <> -1 then
                    printfn "Client %A: Already Registered!" UserID
                    buff <- "Client " + UserID.ToString() + ": Already Registered!"
                else
                    UserName <- (generateRandomString 50)
                    UserPassword <- (generateRandomString 50)
                    printfn "Registering with User Name: %A, Password: %A ..." UserName UserPassword
                    buff <- "Client " + UserID.ToString() + ": Already Registered!"
                    let request = {Mode = 0; UserName = UserName; UserID = -1; SUserID = -1; QueryTag = ""; UserPassword = UserPassword; Tweet = ""; Tag = ""}
                    let json = Json.serialize request
                    server <! json
            elif s = "twt" then
                if UserID = -1 then
                    printfn "Client %A: Not Registered!" UserID
                    buff <- "Client %A: Not Registered!"
                else
                    let c = generateRandomString 100
                    let t = generateRandomString 5
                    
                    let request = {Mode = 1; UserName = UserName; UserID = UserID; SUserID = -1; QueryTag = ""; UserPassword = UserPassword; Tweet = c; Tag = t}
                    printfn "Send a tweet with Tag %A: %A" t c
                    buff <- "Send a tweet with Tag" + t + ": " + c
                    let json = Json.serialize request
                    server <! json
            elif s = "rtt" then
                if UserID = -1 then
                    printfn "Client %A: Not Registered!" UserID
                    buff <- "Client %A: Not Registered!"
                else
                    let rv = r.Next()
                    let request = {Mode = 1; UserName = UserName; UserID = UserID; SUserID = -1; QueryTag = ""; UserPassword = UserPassword; Tweet = localMessage.[rv % (localMessage.Length)].Content; Tag = localMessage.[rv % (localMessage.Length)].Tag}
                    printfn "Send a retweet from user %A: %A" localMessage.[rv % (localMessage.Length)].User localMessage.[rv % (localMessage.Length)].Content
                    buff <- "Send a retweet from user " + (localMessage.[rv % (localMessage.Length)].User).ToString() + ": " + localMessage.[rv % (localMessage.Length)].Content
                    let json = Json.serialize request
                    server <! json
            elif s = "sub" then
                if UserID = -1 then
                    printfn "Client %A: Not Registered!" UserID
                    buff <- "Client %A: Not Registered!"
                else
                    if localMessage.Length = 0 then
                        printfn "Client %A: No Message!" UserID
                    else
                        let mutable subID = -1;
                        while subID = -1 || subID = UserID do
                            subID <- localMessage.[r.Next() % localMessage.Length].User
                        let request = {Mode = 2; UserName = UserName; UserID = UserID; SUserID = subID; QueryTag = ""; UserPassword = UserPassword; Tweet = ""; Tag = ""}
                        printfn "Subscribe User %A" subID
                        buff <- "Subscribe User " + subID.ToString()
                        let json = Json.serialize request
                        server <! json
            elif s = "req" then
                if UserID = -1 then
                    printfn "Client %A: Not Registered!" UserID
                    buff <- "Client %A: Not Registered!"
                else
                    let request = {Mode = 5; UserName = UserName; UserID = UserID; SUserID = -1; QueryTag = ""; UserPassword = UserPassword; Tweet = ""; Tag = ""}
                    printfn "Requesting new messages..."
                    let json = Json.serialize request
                    server <! json
            elif s = "rsb" then
                if UserID = -1 then
                    printfn "Client %A: Not Registered!" UserID
                    buff <- "Client %A: Not Registered!"
                else
                    let request = {Mode = 6; UserName = UserName; UserID = UserID; SUserID = -1; QueryTag = ""; UserPassword = UserPassword; Tweet = ""; Tag = ""}
                    printfn "Requesting new messages from subscribed users..."
                    let json = Json.serialize request
                    server <! json
            elif s = "rtg" then
                if UserID = -1 then
                    printfn "Client %A: Not Registered!" UserID
                    buff <- "Client %A: Not Registered!"
                else
                    let request = {Mode = 7; UserName = UserName; UserID = UserID; SUserID = -1; QueryTag = ""; UserPassword = UserPassword; Tweet = ""; Tag = "init"}
                    printfn "Requesting new messages with Tag: init..."
                    let json = Json.serialize request
                    server <! json
            elif s = "rqm" then
                if UserID = -1 then
                    printfn "Client %A: Not Registered!" UserID
                    buff <- "Client %A: Not Registered!"
                else
                    let request = {Mode = 8; UserName = UserName; UserID = UserID; SUserID = -1; QueryTag = ""; UserPassword = UserPassword; Tweet = ""; Tag = ""}
                    printfn "Requesting all messages sent..."
                    let json = Json.serialize request
                    server <! json
            else
                let l = Json.deserialize<list<Message>> s
                printfn "Client %A: New Messages Received!" UserID
                buff <- ""
                for i in 0 .. (l.Length - 1) do
                    buff <- buff + "Message " + (i+1).ToString() + " from User " + l.[i].User.ToString() + " with Tag " + l.[i].Tag + ": " + l.[i].Content + "\n"
                    printfn "Message %A from User %A with Tag %A: %A " (i+1) l.[i].User l.[i].Tag l.[i].Content
                localMessage <- localMessage @ l
                
        | _ -> ()
            
        return! loop()
    }
    loop()




/// An example of explictly fetching websocket errors and handling them in your codebase.



(*
  choose [
    path "/websocket" >=> handShake ws
    path "/websocketWithSubprotocol" >=> handShakeWithSubprotocol (chooseSubprotocol "test") ws
    path "/websocketWithError" >=> handShake wsWithErrorHandling
    GET >=> choose [ path "/" >=> file "index.html"; browseHome ]
    NOT_FOUND "Found no handlers." ]
*)
[<EntryPoint>]
let main _ =
    
    let server = system.ActorSelection(addr)
    let client =
        spawn system "client" clientActor
    
    client <! server

    let ws (webSocket : WebSocket) (context: HttpContext) =
        socket {
    
            let mutable loop = true
    
            while loop do
    
                let! msg = webSocket.read()
          
                match msg with
    
                | (Text, data, true) ->
    
                    let input = UTF8.toString data

                    if input = "-1" then loop <- false
                    elif input = "0" then
                        client <! "reg"
                    elif input = "1" then
                        client <! "twt"
                    elif input = "2" then
                        client <! "rtt"
                    elif input = "3" then
                        client <! "sub"
                    elif input = "4" then
                        client <! "req"
                    elif input = "5" then
                        client <! "rsb"
                    elif input = "6" then
                        client <! "rtg"
                    elif input = "7" then
                        client <! "rqm"
                    else printfn "wrong input"
                    printfn "Activity: %s Start" input
                    System.Threading.Thread.Sleep(200)

                    
                    let response = buff
                    let byteResponse =
                        response
                        |> System.Text.Encoding.ASCII.GetBytes
                        |> ByteSegment
    
                    
                    do! webSocket.send Opcode.Text byteResponse true
    
                | (Close, _, _) ->
                    let emptyResponse = [||] |> ByteSegment
                    do! webSocket.send Close emptyResponse true
                    printfn "End of this activity!"
    
                    //loop <- false
    
                | _ -> printfn "dddddddddddd"
        }

    let wsWithErrorHandling (webSocket : WebSocket) (context: HttpContext) = 
       
       let exampleDisposableResource = { new IDisposable with member __.Dispose() = printfn "Resource needed by websocket connection disposed" }
       let websocketWorkflow = ws webSocket context
       
       async {
        let! successOrError = websocketWorkflow
        match successOrError with
        // Success case
        | Choice1Of2() -> ()
        // Error case
        | Choice2Of2(error) ->
            // Example error handling logic here
            printfn "Error: [%A]" error
            exampleDisposableResource.Dispose()
            
        return successOrError
       }

    let app : WebPart = handShake ws
    startWebServer { defaultConfig with logger = Targets.create Verbose [||] } app
    0