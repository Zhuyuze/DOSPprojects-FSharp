open System
open Akka.FSharp
open Akka.Actor


let r = System.Random()
let system = System.create "my-system" (Configuration.load())

type Record =
    {
        InfoString: string
        Key: int
    }


type NodeInfo = 
    {
        NodeKey: int
        NodeRef: IActorRef
    }
    

type SetupInfo =
    {
        SelfKey: int
        SNodeInfo: NodeInfo
        PNodeInfo: NodeInfo
        FNodeInfo: List<NodeInfo>
    }


let from whom =
    sprintf "from %s" whom

[<EntryPoint>]
let main argv =
    let message = from "F#" // Call the function
    printfn "Hello world %s" message
    let numNodes = 24
    let numRequests = 3
    let m = 8
    let maxNum = 1 <<< m

    let chordActor (mailbox: Actor<_>) =
        let mutable Key = 0
        let mutable successor = {NodeKey = 0; NodeRef = mailbox.Self}
        let mutable preceding = {NodeKey = 0; NodeRef = mailbox.Self}
        let mutable fingerTable = []
        let index = 0
        let recordArray = 
            [| 
                for i in 0 .. 16 do
                    yield {InfoString = ""; Key = -1}
            |]

        let closestPrecedingNode id =
            printfn "%d %d %d" Key preceding.NodeKey successor.NodeKey
            System.Threading.Thread.Sleep(500)
            let mutable tmp = -1
            for i = m-1 downto 0 do
                let idd = if id < Key then id + maxNum else id
                let a = if fingerTable.[i].NodeKey < Key then fingerTable.[i].NodeKey + maxNum else fingerTable.[i].NodeKey
                //printfn "%d %d" a idd
                if tmp = -1 && a > Key && a < idd then
                    tmp <- i
            printfn "%d" tmp
            if tmp = -1 then
                mailbox.Self
            else 
                fingerTable.[tmp].NodeRef


        let rec loop() = actor {
            let! message = mailbox.Receive()
            let sender = mailbox.Sender()
            match box message with
            | :? int as id -> 
            (*
                if id > Key && id <= successor.NodeKey then
                    printfn "found! %d %d" Key successor.NodeKey
            *)
                let target = closestPrecedingNode id
                if (target) = mailbox.Self then
                    printfn "found! %d %d" Key successor.NodeKey
                else
                    target <! id
            | :? SetupInfo as info ->
                Key <- info.SelfKey
                successor <- info.SNodeInfo
                preceding <- info.PNodeInfo
                fingerTable <- info.FNodeInfo
            | :? string as x -> 
                printfn "%A" x
            | _ -> ()
                
            return! loop()
        }
        loop()
    
    let rands = [
        for i in 1 .. numNodes do
            yield (r.Next() % maxNum)
    ]
    let numList = List.sort rands

    let actorList = [
        for i in 0 .. (numNodes - 1) do
            let name = "Actor" + i.ToString()
            let temp = spawn system name chordActor
            yield temp
    ]

    for i in 0 .. (numNodes - 1) do
        let FList = [
            for j in 0 .. (m-1) do
                let tmp = 1 <<< j
                let mutable mark = -1;
                for k in 1 .. numNodes do
                    let a = if i + k >= numNodes then numList.[(i+k) % numNodes] + maxNum else numList.[(i+k) % numNodes]
                    if a >= tmp + numList.[i] && mark = -1 then
                        mark <- (i+k) % numNodes
                let temp = {NodeKey = numList.[mark]; NodeRef = actorList.[mark]}
                yield temp
        ]
         
        let myInfo = {SelfKey = numList.[i]; SNodeInfo = {NodeKey = numList.[(i+1) % numNodes]; NodeRef = actorList.[(i+1) % numNodes]}; PNodeInfo = {NodeKey = numList.[(i+numNodes-1) % numNodes]; NodeRef = actorList.[(i+numNodes-1) % numNodes]}; FNodeInfo = FList}
        actorList.[i] <! myInfo


    printfn "!!!!"
    actorList.[23] <! 87


    
    System.Console.ReadLine() |> ignore



    0 // return an integer exit code