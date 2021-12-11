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


type UserInfo =
    {
        UserName: string
        UserPassword: string
        mutable UserMessage: list<int>
        mutable UserSubscription: list<int>
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



let config =
    Configuration.parse // Change Server IP here
        @"akka {
            actor.provider = ""Akka.Remote.RemoteActorRefProvider, Akka.Remote""
            remote.helios.tcp {
                hostname = ""127.0.0.1""
                port = 34567
            }
        }"
let system = System.create "RemoteFSharp" config


let r = System.Random()


let serverActor (mailbox: Actor<_>) =
    let mutable Users:list<UserInfo> = []
    let mutable Messages:list<Message> = []
    let rec loop() = actor {
        let! message = mailbox.Receive()
        let sender = mailbox.Sender()
        match box message with
        | :? requestMessage as rm ->
            if rm.Mode = 0 then // register
                Users <- {UserName = rm.UserName; UserPassword = rm.UserPassword; UserMessage = []; UserSubscription = []} :: Users
                sender <! (Users.Length - 1) //allocate user id
            elif rm.Mode = 1 then // tweet
                Messages <- {Content = rm.Tweet; Tag = rm.Tweet; User = rm.UserID} :: Messages
                Users.[rm.UserID].UserMessage <- (Messages.Length - 1) :: Users.[rm.UserID].UserMessage
                if Messages.Length % 100 = 0 then
                    printfn "[Server: Now we have %A users with %A messages!]" (Users.Length - 2) Messages.Length
                    for i in 0 .. (Users.Length - 1) do
                        printfn "[Server: User %A has %A messages and %A subscriptions.]" i Users.[i].UserMessage.Length Users.[i].UserSubscription.Length
                    System.Threading.Thread.Sleep(50)
            elif rm.Mode = 2 then // subscription
                Users.[rm.UserID].UserSubscription <- rm.SUserID :: Users.[rm.UserID].UserSubscription
            elif rm.Mode = 5 then // request 5 tweets || connected
                if Messages.Length < 5 then
                    let mutable returnList:list<Message> = []
                    for i = 1 to Messages.Length do
                        returnList <- Messages.[i-1] :: returnList
                    sender <! returnList
                else
                    let mutable returnList:list<Message> = []
                    for i in 1 .. 5 do
                        returnList <- Messages.[r.Next() % Messages.Length] :: returnList
                    sender <! returnList
            elif rm.Mode = 6 then // request tweets from subscription
                let mutable returnList:list<Message> = []
                for i in 0 .. (Users.[rm.UserID].UserSubscription.Length - 1) do
                    for j in 0 .. (Messages.Length - 1) do
                        if Messages.[j].User = Users.[rm.UserID].UserSubscription.[i] then
                            returnList <- Messages.[j] :: returnList
                sender <! returnList
            elif rm.Mode = 7 then // query tweets with Tag
                let mutable returnList:list<Message> = []
                for i in 0 .. Messages.Length - 1 do
                    if Messages.[i].Tag = rm.Tag then
                        returnList <- Messages.[i] :: returnList
                sender <! returnList
        | :? string as s -> 
            if s = "init" then
                Users <- {UserName = "User1"; UserPassword = "User1"; UserMessage = [1; 2; 3]; UserSubscription = [1]} :: Users
                Users <- {UserName = "User2"; UserPassword = "User2"; UserMessage = [4; 5; 6; 7]; UserSubscription = [0]} :: Users
                Messages <- {Content = "abcdefg"; Tag = "init"; User = 0} :: Messages
                Messages <- {Content = "abcdefg"; Tag = "init"; User = 0} :: Messages
                Messages <- {Content = "abcdefg"; Tag = "ini"; User = 0} :: Messages
                Messages <- {Content = "abcdefg"; Tag = "init"; User = 1} :: Messages
                Messages <- {Content = "abcdefg"; Tag = "ini"; User = 1} :: Messages
                Messages <- {Content = "abcdefg"; Tag = "ini"; User = 1} :: Messages
                Messages <- {Content = "abcdefg"; Tag = "init"; User = 1} :: Messages
            else 
                printfn "%A" s
                let rm = Json.deserialize<requestMessage> s
                if rm.Mode = 0 then // register
                    Users <- Users @ [{UserName = rm.UserName; UserPassword = rm.UserPassword; UserMessage = []; UserSubscription = []}]
                    printfn "A new user named %A registered!" rm.UserName
                    sender <! (Users.Length - 1) //allocate user id
                elif rm.Mode = 1 then // tweet
                    Messages <- {Content = rm.Tweet; Tag = rm.Tag; User = rm.UserID} :: Messages
                    Users.[rm.UserID].UserMessage <- (Messages.Length - 1) :: Users.[rm.UserID].UserMessage
                    printfn "Message with Tag %A from User %A received!" rm.Tag rm.UserID
                    System.Threading.Thread.Sleep(500)
                    //if Messages.Length % 100 = 0 then
                    printfn "[Server: Now we have %A users with %A messages!]" Users.Length Messages.Length
                    for i in 0 .. (Users.Length - 1) do
                        printfn "[Server: User %A has %A messages and %A subscriptions.]" i Users.[i].UserMessage.Length Users.[i].UserSubscription.Length
                    
                elif rm.Mode = 2 then // subscription
                    Users.[rm.UserID].UserSubscription <- rm.SUserID :: Users.[rm.UserID].UserSubscription
                    printfn "User %A has subscribed User %A" rm.UserID rm.SUserID
                elif rm.Mode = 5 then // request 5 tweets || connected
                    if Messages.Length < 5 then
                        let mutable returnList:list<Message> = []
                        for i = 1 to Messages.Length do
                            returnList <- Messages.[i-1] :: returnList
                        let json = Json.serialize returnList
                        sender <! json
                    else
                        let mutable returnList:list<Message> = []
                        for i in 1 .. 5 do
                            returnList <- Messages.[r.Next() % Messages.Length] :: returnList
                        let json = Json.serialize returnList
                        printfn "Send 5 tweets to User %A" rm.UserID
                        sender <! json
                elif rm.Mode = 6 then // request tweets from subscription
                    let mutable returnList:list<Message> = []
                    for i in 0 .. (Users.[rm.UserID].UserSubscription.Length - 1) do
                        for j in 0 .. (Messages.Length - 1) do
                            if Messages.[j].User = Users.[rm.UserID].UserSubscription.[i] then
                                returnList <- Messages.[j] :: returnList
                    let json = Json.serialize returnList
                    printfn "Send subscribed tweets to User %A" rm.UserID
                    sender <! json
                elif rm.Mode = 7 then // query tweets with Tag
                    let mutable returnList:list<Message> = []
                    for i in 0 .. Messages.Length - 1 do
                        if Messages.[i].Tag = rm.Tag then
                            returnList <- Messages.[i] :: returnList
                    let json = Json.serialize returnList
                    printfn "Send tweets with Tag %A to User %A" rm.Tag rm.UserID
                    sender <! json
                elif rm.Mode = 8 then // query tweets with Tag
                    let mutable returnList:list<Message> = []
                    for i in 0 .. Messages.Length - 1 do
                        if Messages.[i].User = rm.UserID then
                            returnList <- Messages.[i] :: returnList
                    let json = Json.serialize returnList
                    printfn "Send tweets of himself to User %A" rm.UserID
                    sender <! json

        | _ -> ()
            
        return! loop()
    }
    loop()


[<EntryPoint>]
let main argv =
    

    let server =
        spawn system "TweeterServer" serverActor

    server <! "init"


    Console.ReadLine() |> ignore


    0 // return an integer exit code