// Learn more about F# at http://docs.microsoft.com/dotnet/fsharp

open System
open Akka
open Akka.FSharp
open Akka.Actor
open Akka.Configuration
open FSharp.Json


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
                else
                    UserName <- (generateRandomString 50)
                    UserPassword <- (generateRandomString 50)
                    printfn "Registering with User Name: %A, Password: %A ..." UserName UserPassword
                    let request = {Mode = 0; UserName = UserName; UserID = -1; SUserID = -1; QueryTag = ""; UserPassword = UserPassword; Tweet = ""; Tag = ""}
                    let json = Json.serialize request
                    server <! json
            elif s = "twt" then
                if UserID = -1 then
                    printfn "Client %A: Not Registered!" UserID
                else
                    let c = generateRandomString 100
                    let t = generateRandomString 5
                    let request = {Mode = 1; UserName = UserName; UserID = UserID; SUserID = -1; QueryTag = ""; UserPassword = UserPassword; Tweet = c; Tag = t}
                    printfn "Send a tweet with Tag %A: %A" t c
                    let json = Json.serialize request
                    server <! json
            elif s = "rtt" then
                if UserID = -1 then
                    printfn "Client %A: Not Registered!" UserID
                else
                    let rv = r.Next()
                    let request = {Mode = 1; UserName = UserName; UserID = UserID; SUserID = -1; QueryTag = ""; UserPassword = UserPassword; Tweet = localMessage.[rv % (localMessage.Length)].Content; Tag = localMessage.[rv % (localMessage.Length)].Tag}
                    printfn "Send a retweet from user %A: %A" localMessage.[rv % (localMessage.Length)].User localMessage.[rv % (localMessage.Length)].Content
                    let json = Json.serialize request
                    server <! json
            elif s = "sub" then
                if UserID = -1 then
                    printfn "Client %A: Not Registered!" UserID
                else
                    if localMessage.Length = 0 then
                        printfn "Client %A: No Message!" UserID
                    else
                        let mutable subID = -1;
                        while subID = -1 || subID = UserID do
                            subID <- localMessage.[r.Next() % localMessage.Length].User
                        let request = {Mode = 2; UserName = UserName; UserID = UserID; SUserID = subID; QueryTag = ""; UserPassword = UserPassword; Tweet = ""; Tag = ""}
                        printfn "Subscribe User %A" subID
                        let json = Json.serialize request
                        server <! json
            elif s = "req" then
                if UserID = -1 then
                    printfn "Client %A: Not Registered!" UserID
                else
                    let request = {Mode = 5; UserName = UserName; UserID = UserID; SUserID = -1; QueryTag = ""; UserPassword = UserPassword; Tweet = ""; Tag = ""}
                    printfn "Requesting new messages..."
                    let json = Json.serialize request
                    server <! json
            elif s = "rsb" then
                if UserID = -1 then
                    printfn "Client %A: Not Registered!" UserID
                else
                    let request = {Mode = 6; UserName = UserName; UserID = UserID; SUserID = -1; QueryTag = ""; UserPassword = UserPassword; Tweet = ""; Tag = ""}
                    printfn "Requesting new messages from subscribed users..."
                    let json = Json.serialize request
                    server <! json
            elif s = "rtg" then
                if UserID = -1 then
                    printfn "Client %A: Not Registered!" UserID
                else
                    let request = {Mode = 7; UserName = UserName; UserID = UserID; SUserID = -1; QueryTag = ""; UserPassword = UserPassword; Tweet = ""; Tag = "init"}
                    printfn "Requesting new messages with Tag: init..."
                    let json = Json.serialize request
                    server <! json
            elif s = "rqm" then
                if UserID = -1 then
                    printfn "Client %A: Not Registered!" UserID
                else
                    let request = {Mode = 8; UserName = UserName; UserID = UserID; SUserID = -1; QueryTag = ""; UserPassword = UserPassword; Tweet = ""; Tag = ""}
                    printfn "Requesting all messages sent..."
                    let json = Json.serialize request
                    server <! json
            else
                let l = Json.deserialize<list<Message>> s
                printfn "Client %A: New Messages Received!" UserID
                for i in 0 .. (l.Length - 1) do
                    printfn "Message %A from User %A with Tag %A: %A " (i+1) l.[i].User l.[i].Tag l.[i].Content
                localMessage <- localMessage @ l
                
        | _ -> ()
            
        return! loop()
    }
    loop()


[<EntryPoint>]
let main argv =
    
    let addr = "akka.tcp://RemoteFSharp@127.0.0.1:34567/user/TweeterServer"
    let server = system.ActorSelection(addr)
    let client =
        spawn system "client" clientActor
    
    client <! server
    printfn "Press any key to register!"
    Console.ReadLine() |> ignore

    client <! "reg"

    Console.ReadLine() |> ignore

    let mutable flag = true
    while flag do
        printfn "Functions:"
        printfn "0: quit"
        printfn "1: send a tweet"
        printfn "2: send a retweet"
        printfn "3: subscribe a user"
        printfn "4: request new messages"
        printfn "5: request messages from subscription"
        printfn "6: request messages with a tag"
        printfn "7: request all messages of yourself"
        
        let input = Console.ReadLine()
        if input = "0" then flag <- false
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
        System.Threading.Thread.Sleep(100)


    0 // return an integer exit code