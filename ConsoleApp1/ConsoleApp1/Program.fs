open Akka.FSharp
open Akka.IO
open System.Net
open System.Text
open Akka.Actor
open Akka.Remote
open Akka.Configuration
  

let system = System.create "my-system" (Configuration.load())


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


let rec findSHAinlength (n:int) (s:string) (count:int) (a:IActorRef)=
    if count > 0 then
        let newcount = count-1
        for (j:char) in char(32) .. char(126) do
            let temp = s + j.ToString()
            findSHAinlength n temp newcount a
    else
        for (i:char) in char(32) .. char(126) do
            let temp = s + i.ToString()
            let sha = sha256 temp
            if fitzeroes n sha then
                //printfn "%s\t%d\t%s" temp temp.Length sha
                let output = temp + "\t" + temp.Length.ToString() + "\t" + sha
                a <! output



let findSHA (n:int) (s:string) (count:int) (a:IActorRef)=
    for i in 0 .. count do
        printfn "%d" i
        findSHAinlength n s i a



let findSHAWithActorList (n:int) (s:string) (count:int) (l:List<Akka.Actor.IActorRef>) =
    for i in 0 .. count do
        let record = {CurrentString=s; AllocatedLength=i; NumOfZeroes=n}
        let aindex = i % l.Length
        //printfn "Actor %d is working on length %d" aindex i
        l.Item(aindex) <!  record


let hashActor (mailbox: Actor<_>) =
    let rec loop() = actor {
        let! message = mailbox.Receive()
        let sender = mailbox.Sender()
        match box message with
        | :? int as x -> 
            printfn "%A" x
        | :? string as x -> 
            printfn "%A" x
        | :? IActorRef as a ->
            printfn "%s is activated" mailbox.Self.Path.Name
            a <! true
        | :? InfoRecord as c ->
            let s = mailbox.Self.Path.Name + " is working on task with allocated length: " + c.AllocatedLength.ToString()
            sender <! s
            findSHAinlength c.NumOfZeroes c.CurrentString c.AllocatedLength sender
            sender <! true
        | _ -> ()
            
        return! loop()
    }
    loop()


[<EntryPoint>]
let main argv =
    let input = 4 // num of prefix 0s (+1)
    let lengthRange = 16 // max additional string length
    let numOfActors = 8 // total actors to create
    let s = "Yuze Zhu" // Initial string

    (*
    let actorList = [
        for i in 1 .. numOfActors do
            let name = "Actor" + i.ToString()
            let temp = 
                spawn system name
                    (fun mailbox ->
                        let rec loop() = actor {
                            let! message = mailbox.Receive()
            
                            match message with
                            | Myint x -> 
                                printfn "%A" x
                            | Mystring x -> 
                                printfn "%A" x
                            | Myinfo c ->
                                printfn "%s is working on length %d" name c.AllocatedLength
                                findSHAinlength c.NumOfZeroes c.CurrentString c.AllocatedLength
            
                            return! loop()
                        }
                        loop()
                    )
            yield temp
    ]
    *)


    let actorList = [
        for i in 1 .. numOfActors do
            let name = "Actor" + i.ToString()
            let temp = 
                spawn system name hashActor
            yield temp
    ]

    

    let server = 
        spawn system "server"
            (fun mailbox ->
                //let mutable count = 0
                let mutable totallength = 0
                let mutable currentlength = 0
                let mutable nOzs = 0
                let mutable prefixString = ""
                let rec loop() = actor {
                    let! message = mailbox.Receive()
                    let sender = mailbox.Sender()
                    
    
                    match box message with
                    | :? string as message -> 
                        //count <- count + 1
                        printfn "%s" message
                    | :? bool as signal ->
                        if signal && currentlength < totallength then
                            currentlength <- currentlength + 1
                            let record = {CurrentString=prefixString; AllocatedLength=currentlength; NumOfZeroes=nOzs}
                            sender <!  record

                    | :? MyMessage as message ->
                        printfn "Mission start!"
                        totallength <- message.MyInfoRecord.AllocatedLength
                        nOzs <- message.MyInfoRecord.NumOfZeroes
                        prefixString <- message.MyInfoRecord.CurrentString
                        (*
                        for i in 0 .. message.MyInfoRecord.AllocatedLength do 
                            let k = i % message.ListOfActors.Length
                            let record = {CurrentString=message.MyInfoRecord.CurrentString; AllocatedLength=i; NumOfZeroes=message.MyInfoRecord.NumOfZeroes}
                            message.ListOfActors.Item(k) <! Myinfo record
                        *)
                    | _ -> ()
    
                    return! loop()
                }
                loop()
            )


    let tempRecord = {CurrentString=s; AllocatedLength=lengthRange; NumOfZeroes=input}
    let newMessage = {ListOfActors=actorList; MyInfoRecord=tempRecord}




    //use an actor as server to allocate tasks to actors in the list
    server <! newMessage
    System.Console.ReadLine() |> ignore
    for i in 0 .. (actorList.Length - 1) do
        actorList.Item(i) <! server
        System.Threading.Thread.Sleep(1000)


    //without actors
    //findSHA input s lengthRange server




    //use a function to allocate tasks to actors in the list
    //findSHAWithActorList input s lengthRange actorList 


    System.Console.ReadLine() |> ignore

    0 // Return an integer exit code