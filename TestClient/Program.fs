// Learn more about F# at http://docs.microsoft.com/dotnet/fsharp

open Akka.FSharp
open Akka.IO
open System.Net
open System.Text
open Akka.Actor
open Akka.Remote
open Akka.Configuration
(*
[<Struct>]
type InfoRecord =
    {
        CurrentString: string
        NumOfZeroes: int
        AllocatedLength: int
    }


type MyMessage =
    {
        ListOfActors: list<IActorRef>
        MyInfoRecord: InfoRecord
    }
*)

let sha256 (s:string) =
    let b = System.Text.Encoding.ASCII.GetBytes(s)
    let bsha256 = System.Security.Cryptography.SHA256.Create().ComputeHash(b)
    let hex = Array.map (fun (x : byte) -> System.String.Format("{0:X2}", x)) bsha256
    let output = String.concat System.String.Empty hex
    output


let fitzeroes (n:int) (s:string) =
    let mutable b = true
    for i in 0 .. n do
        if not (s.[i..i].Equals("0")) then
            b <- false
    b


let rec findSHAinlengthrandom (n:int) (s:string) (count:int) (a:IActorRef) =
    let r = System.Random()
    let mutable temp = s
    let t = if count > 4 then 2147483647 else int (94.0 ** float count)
    for l in 0 .. t do
        for  i in 0 .. count do
            temp <- temp + char(r.Next() % 95 + 32).ToString()
        let sha = sha256 temp
        if fitzeroes n sha then
            //printfn "%s\t%d\t%s" temp temp.Length sha
            let output = temp + "\t" + temp.Length.ToString() + "\t" + sha
            a <! output


let rec findSHAinlength (n:int) (s:string) (count:int) (a:IActorRef)=
    if count > 0 then
        let newcount = count-1
        for (j:char) in char(32) .. char(126) do
            let temp = s + j.ToString()
            findSHAinlength n temp newcount a
    else
        let sha = sha256 s
        if fitzeroes n sha then
            //printfn "%s\t%d\t%s" temp temp.Length sha
            let output = s + "\t" + s.Length.ToString() + "\t" + sha
            a <! output



let findSHA (n:int) (s:string) (count:int) (a:IActorRef)=
    for i in 0 .. count do
        printfn "%d" i
        findSHAinlength n s i a


(*
let findSHAWithActorList (n:int) (s:string) (count:int) (l:List<Akka.Actor.IActorRef>) =
    for i in 0 .. count do
        let record = {CurrentString=s; AllocatedLength=i; NumOfZeroes=n}
        let aindex = i % l.Length
        //printfn "Actor %d is working on length %d" aindex i
        l.Item(aindex) <! record
*)

let hashActor (mailbox: Actor<_>) =
    let rec loop() = actor {
        let! message = mailbox.Receive()
        let sender = mailbox.Sender()
        match box message with
        | :? int as x -> 
            printfn "%A" x
        | :? string as x -> 
            let l = x.Split '\n'
            let noz = l.[2] |> int
            let cs = l.[0]
            let al = l.[1] |> int
            let s = mailbox.Self.Path.Name + " is working on task with allocated length: " + l.[1]
            sender <! s
            findSHAinlength noz cs al sender
            sender <! true
            //printfn "%A" x
        | :? ActorSelection as a ->
            //a <! "SS\nSS"
            printfn "%s is activated" mailbox.Self.Path.Name
            a <! true
        (*
        | :? InfoRecord as c ->
            let s = mailbox.Self.Path.Name + " is working on task with allocated length: " + c.AllocatedLength.ToString()
            sender <! s
            //findSHAinlengthrandom c.NumOfZeroes c.CurrentString c.AllocatedLength sender
            findSHAinlength c.NumOfZeroes c.CurrentString c.AllocatedLength sender
            sender <! true
        *)
        | _ -> ()
            
        return! loop()
    }
    loop()


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



[<EntryPoint>]
let main args =
    let ip = if args.Length > 0 then args.[0] else "127.0.0.1"
    let numOfActors = 4 // Num of working actors in client
    let actorList = [
        for i in 1 .. numOfActors do
            let name = "ClientActor" + i.ToString()
            let temp = 
                spawn system name hashActor
            yield temp
    ]

    let addr = "akka.tcp://RemoteFSharp@" + ip + ":34567/user/EchoServer"
    let server = system.ActorSelection(addr)

    for i in 0 .. (actorList.Length - 1) do
        actorList.Item(i) <! server
        System.Threading.Thread.Sleep(100)



    System.Console.ReadLine() |> ignore

    0






//let (task: Async<obj>) = echoClient <? "F#!"
//let response = Async.RunSynchronously (task, 1000)
//printfn "Reply from remote %s" (string(response))