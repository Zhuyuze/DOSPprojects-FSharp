open System
open Akka.FSharp
open Akka.Actor


let system = System.create "my-system" (Configuration.load())


type MyMessage =
    {
        WMessage: float
        SMessage: float
        Counter: int
        TargetSet: List<IActorRef>
        TargetNum: int
        Keeper: IActorRef
        SelfIndex: int
        Mode: string
        Algo: string
    }


type MyCubeMessage =
    {
        WMessage: float
        SMessage: float
        Counter: int
        CubeSet: List<List<List<IActorRef>>>
        Width: int
        Keeper: IActorRef
        SelfX: int
        SelfY: int
        SelfZ: int
        Mode: string
        Algo: string
    }


let actorFunc (mailbox: Actor<_>) =
    let mutable w = 1.0
    let mutable s = 0.0
    let mutable Gcounter = 0
    let rec loop() = actor {
        let! message = mailbox.Receive()
        let sender = mailbox.Sender()
        match box message with
        | :? string as s ->
            printfn "%s: %A" mailbox.Self.Path.Name s
        | :? int as x ->
            s <- s + float x
        | :? MyMessage as m ->
            //printfn "%s received : w : %f s : %f" mailbox.Self.Path.Name m.WMessage m.SMessage
            if m.Algo = "push-sum" then
                let r = System.Random()
                let mutable c = m.Counter
                let preW = w
                let preS = s
                w <- w + m.WMessage
                s <- s + m.SMessage
                if (s / w - preS / preW) <= 0.0000000001 && (s / w - preS / preW) >= -0.0000000001 then
                    c <- c + 1
                else
                    c <- 0
                if c >= 3 then
                    m.Keeper <! true
                    printfn "Finished!"
                else
                    w <- w / 2.0
                    s <- s / 2.0
                    if m.Mode = "Full" then
                        let mutable next = r.Next() % m.TargetNum
                        while next = m.SelfIndex do
                            next <- r.Next() % m.TargetNum
                        let newMessage = {WMessage = w; SMessage = s; Counter = c; TargetSet = m.TargetSet; TargetNum = m.TargetNum; Keeper = m.Keeper; SelfIndex = next; Mode = "Full"; Algo = "push-sum"}
                        m.TargetSet.Item(next) <! newMessage
                    elif m.Mode = "Line" then
                        let flag = r.Next() % 2;
                        let mutable next = m.SelfIndex
                        if (flag = 1 && next < m.TargetNum - 1) || next <= 0 then
                            next <- next + 1
                        else
                            next <- next - 1
                        let newMessage = {WMessage = w; SMessage = s; Counter = c; TargetSet = m.TargetSet; TargetNum = m.TargetNum; Keeper = m.Keeper; SelfIndex = next; Mode = "Line"; Algo = "push-sum"}
                        m.TargetSet.Item(next) <! newMessage

            elif m.Algo = "gossip" then
                //printfn "%s received : gossip: %d" mailbox.Self.Path.Name Gcounter
                let r = System.Random()
                Gcounter <- Gcounter + 1
                if Gcounter >= 10 then
                    m.Keeper <! true
                    printfn "Finished!"
                else
                    if m.Mode = "Full" then
                        let mutable next = r.Next() % m.TargetNum
                        while next = m.SelfIndex do
                            next <- r.Next() % m.TargetNum
                        let newMessage = {WMessage = 0.0; SMessage = 0.0; Counter = 0; TargetSet = m.TargetSet; TargetNum = m.TargetNum; Keeper = m.Keeper; SelfIndex = next; Mode = "Full"; Algo = "gossip"}
                        m.TargetSet.Item(next) <! newMessage
                    elif m.Mode = "Line" then
                        let flag = r.Next() % 2;
                        let mutable next = m.SelfIndex
                        if (flag = 1 && next < m.TargetNum - 1) || next <= 0 then
                            next <- next + 1
                        else
                            next <- next - 1
                        let newMessage = {WMessage = 0.0; SMessage = 0.0; Counter = 0; TargetSet = m.TargetSet; TargetNum = m.TargetNum; Keeper = m.Keeper; SelfIndex = next; Mode = "Line"; Algo = "gossip"}
                        m.TargetSet.Item(next) <! newMessage
            
        | _ -> ()
        return! loop()
    }
    loop()


let cubeActorFunc (mailbox: Actor<_>) =
    let mutable w = 1.0
    let mutable s = 0.0
    let mutable Gcounter = 0
    let rec loop() = actor {
        let! message = mailbox.Receive()
        let sender = mailbox.Sender()
        match box message with
        | :? string as s ->
            printfn "%s: %A" mailbox.Self.Path.Name s
        | :? int as x ->
            s <- s + float x
        | :? MyCubeMessage as m ->
            if m.Algo = "push-sum" then
                //printfn "%s received : w : %f s : %f" mailbox.Self.Path.Name m.WMessage m.SMessage
                //System.Threading.Thread.Sleep(1000)
                let r = System.Random()
                let mutable c = m.Counter
                let preW = w
                let preS = s
                w <- w + m.WMessage
                s <- s + m.SMessage
                if (s / w - preS / preW) <= 0.0000000001 && (s / w - preS / preW) >= -0.0000000001 then
                    c <- c + 1
                else
                    c <- 0
                if c >= 3 then
                    m.Keeper <! true
                    printfn "Finished!"
                else
                    w <- w / 2.0
                    s <- s / 2.0
                    if m.Mode = "3D" then
                        let mutable X = -1
                        let mutable Y = -1
                        let mutable Z = -1
                        while X < 0 || X > m.Width - 1 || Y < 0 || Y > m.Width - 1 || Z < 0 || Z > m.Width - 1 do
                            X <- m.SelfX
                            Y <- m.SelfY
                            Z <- m.SelfZ
                            let flag = r.Next() % 3
                            if flag = 0 then
                                if r.Next() % 2 = 0 then
                                    X <- X + 1
                                else
                                    X <- X - 1
                            elif flag = 1 then
                                if r.Next() % 2 = 0 then
                                    Y <- Y + 1
                                else
                                    Y <- Y - 1
                            else
                                if r.Next() % 2 = 0 then
                                    Z <- Z + 1
                                else
                                    Z <- Z - 1
                        let newMessage = {WMessage = w; SMessage = s; Counter = c; CubeSet = m.CubeSet; Width = m.Width; Keeper = m.Keeper; SelfX = X; SelfY = Y; SelfZ = Z; Mode = "3D"; Algo = "push-sum"}
                        m.CubeSet.Item(X).Item(Y).Item(Z) <! newMessage
                    elif m.Mode = "Imperfect" then
                        let mutable X = -1
                        let mutable Y = -1
                        let mutable Z = -1
                        while X < 0 || X > m.Width - 1 || Y < 0 || Y > m.Width - 1 || Z < 0 || Z > m.Width - 1 do
                            X <- m.SelfX
                            Y <- m.SelfY
                            Z <- m.SelfZ
                            let flag = r.Next() % 4
                            if flag = 0 then
                                if r.Next() % 2 = 0 then
                                    X <- X + 1
                                else
                                    X <- X - 1
                            elif flag = 1 then
                                if r.Next() % 2 = 0 then
                                    Y <- Y + 1
                                else
                                    Y <- Y - 1
                            elif flag = 2 then
                                if r.Next() % 2 = 0 then
                                    Z <- Z + 1
                                else
                                    Z <- Z - 1
                            else
                                while X = m.SelfX && Y = m.SelfY && Z = m.SelfZ do
                                    X <- r.Next() % m.Width
                                    Y <- r.Next() % m.Width
                                    Z <- r.Next() % m.Width
                        let newMessage = {WMessage = w; SMessage = s; Counter = c; CubeSet = m.CubeSet; Width = m.Width; Keeper = m.Keeper; SelfX = X; SelfY = Y; SelfZ = Z; Mode = "Imperfect"; Algo = "push-sum"}
                        m.CubeSet.Item(X).Item(Y).Item(Z) <! newMessage
            elif m.Algo = "gossip" then
                //printfn "%s received : gossip: %d" mailbox.Self.Path.Name Gcounter
                let r = System.Random()
                Gcounter <- Gcounter + 1
                if Gcounter >= 10 then
                    m.Keeper <! true
                    printfn "Finished!"
                else
                    if m.Mode = "3D" then
                        let mutable X = -1
                        let mutable Y = -1
                        let mutable Z = -1
                        while X < 0 || X > m.Width - 1 || Y < 0 || Y > m.Width - 1 || Z < 0 || Z > m.Width - 1 do
                            X <- m.SelfX
                            Y <- m.SelfY
                            Z <- m.SelfZ
                            let flag = r.Next() % 3
                            if flag = 0 then
                                if r.Next() % 2 = 0 then
                                    X <- X + 1
                                else
                                    X <- X - 1
                            elif flag = 1 then
                                if r.Next() % 2 = 0 then
                                    Y <- Y + 1
                                else
                                    Y <- Y - 1
                            else
                                if r.Next() % 2 = 0 then
                                    Z <- Z + 1
                                else
                                    Z <- Z - 1
                        let newMessage = {WMessage = 0.0; SMessage = 0.0; Counter = 0; CubeSet = m.CubeSet; Width = m.Width; Keeper = m.Keeper; SelfX = X; SelfY = Y; SelfZ = Z; Mode = "3D"; Algo = "gossip"}
                        m.CubeSet.Item(X).Item(Y).Item(Z) <! newMessage
                    elif m.Mode = "Imperfect" then
                        let mutable X = -1
                        let mutable Y = -1
                        let mutable Z = -1
                        while X < 0 || X > m.Width - 1 || Y < 0 || Y > m.Width - 1 || Z < 0 || Z > m.Width - 1 do
                            X <- m.SelfX
                            Y <- m.SelfY
                            Z <- m.SelfZ
                            let flag = r.Next() % 4
                            if flag = 0 then
                                if r.Next() % 2 = 0 then
                                    X <- X + 1
                                else
                                    X <- X - 1
                            elif flag = 1 then
                                if r.Next() % 2 = 0 then
                                    Y <- Y + 1
                                else
                                    Y <- Y - 1
                            elif flag = 2 then
                                if r.Next() % 2 = 0 then
                                    Z <- Z + 1
                                else
                                    Z <- Z - 1
                            else
                                while X = m.SelfX && Y = m.SelfY && Z = m.SelfZ do
                                    X <- r.Next() % m.Width
                                    Y <- r.Next() % m.Width
                                    Z <- r.Next() % m.Width
                        let newMessage = {WMessage = 0.0; SMessage = 0.0; Counter = 0; CubeSet = m.CubeSet; Width = m.Width; Keeper = m.Keeper; SelfX = X; SelfY = Y; SelfZ = Z; Mode = "Imperfect"; Algo = "gossip"}
                        m.CubeSet.Item(X).Item(Y).Item(Z) <! newMessage
        | _ -> ()
        return! loop()
    }
    loop()


[<EntryPoint>]
let main argv =
    let mutable mode = "imp3D"
    let mutable numOfActors = 1000
    let mutable algo = "push-sum"
    if argv.Length > 0 then
        numOfActors <- int argv.[0]
        mode <- argv.[1]
        algo <- argv.[2]

    
    let actorList = [
        for i in 1 .. numOfActors do
            let name = "Actor" + i.ToString()
            let temp = spawn system name actorFunc
            temp <! i
            yield temp
    ]


    let side = int ((float numOfActors) ** (1.0/3.0)) + 1
    //printfn "%d" side
    let cubeActorList = [
        for i in 1 .. side do
            let templistC = [
                for j in 1 .. side do
                    let tempListR = [
                        for k in 1 .. side do
                            let name = "Actor" + i.ToString() + "," + j.ToString() + "," + k.ToString()
                            let temp = spawn system name cubeActorFunc
                            temp <! i + j + k
                            yield temp
                    ]
                    yield tempListR
            ]
            yield templistC
    ]

    
    let timeKeeper =
    
        spawn system "TimeKeeper"
            (fun mailbox ->
                let timer = new System.Diagnostics.Stopwatch()
                let rec loop() = actor {
                    let! message = mailbox.Receive()
                    let sender = mailbox.Sender()
                    match box message with
                    | :? string as s ->
                        if s = "Full" then
                            let newMessage = {WMessage = 0.0; SMessage = 0.0; Counter = 0; TargetSet = actorList; TargetNum = numOfActors; Keeper = mailbox.Self; SelfIndex = numOfActors / 2; Mode = "Full"; Algo = algo}
                            actorList.Item(numOfActors / 2) <! newMessage
                            timer.Start()
                        elif s = "Line" then
                            let newMessage = {WMessage = 0.0; SMessage = 0.0; Counter = 0; TargetSet = actorList; TargetNum = numOfActors; Keeper = mailbox.Self; SelfIndex = numOfActors / 2; Mode = "Line"; Algo = algo}
                            actorList.Item(numOfActors / 2) <! newMessage
                            timer.Start()
                        elif s = "3D" then
                            let newMessage = {WMessage = 0.0; SMessage = 0.0; Counter = 0; CubeSet = cubeActorList; Width = side; Keeper = mailbox.Self; SelfX = side / 2; SelfY = side / 2; SelfZ = side / 2; Mode = "3D"; Algo = algo}
                            cubeActorList.Item(side / 2).Item(side / 2).Item(side / 2) <! newMessage
                            timer.Start()
                        elif s = "Imperfect" then
                            let newMessage = {WMessage = 0.0; SMessage = 0.0; Counter = 0; CubeSet = cubeActorList; Width = side; Keeper = mailbox.Self; SelfX = side / 2; SelfY = side / 2; SelfZ = side / 2; Mode = "Imperfect"; Algo = algo}
                            cubeActorList.Item(side / 2).Item(side / 2).Item(side / 2) <! newMessage
                            timer.Start()
                    | :? bool as flag ->
                        timer.Stop()
                        printfn "Time consumed: %dms" (timer.ElapsedMilliseconds)
                        
                    | _ -> ()
    
                    return! loop()
                }
                loop()
            )
    
    if mode = "full" then
        timeKeeper <! "Full"
    elif mode = "3D" then
        timeKeeper <! "3D"
    elif mode = "line" then
        timeKeeper <! "Line"
    elif mode = "imp3D" then
        timeKeeper <! "Imperfect"
    System.Console.ReadLine() |> ignore

    0 // return an integer exit code