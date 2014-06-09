namespace ReactiveGameServer.Client

open Nessos.UnionArgParser

type Arguments = 
    | [<Mandatory>] IpAddress of string
    | [<Mandatory>] LocalPort of int
    | [<Mandatory>] Port of int
    | [<Mandatory>] Username of string
    interface IArgParserTemplate with
        member this.Usage = 
            match this with
            | LocalPort _ -> "Local port to open the connection from"
            | IpAddress _ -> "Remote server ip address"
            | Port _ -> "Remote open port"
            | Username _ -> "Username with which to register this client"

module Core = 
    open System
    open System.Drawing
    open System.Net
    open System.Net.Sockets
    open System.Windows.Forms
    open ReactiveGameServer.Shared.Model
    open ReactiveGameServer.Shared.Network
    
    type ConnectionStatus = 
        | Success of UdpClient * Guid * (int * int) * PlayerInfo list
        | Failure
    
    // This wrapper type here is so that we can merge 2 observables both from different sources
    // and of different types.
    type ObservableSource<'a, 'b> = 
        | Local of 'a
        | Remote of 'b
    
    // We perform the handshake in a non reactive way as we need to run this state machine
    let PerformHandshake (localPort : int) endPoint username = 
        let client = new UdpClient(localPort)
        client.Connect(endPoint)
        try 
            let msg = 
                async { 
                    let connection = Connect(username)
                    let msg = Serialize connection
                    do! client.SendAsync(msg, msg.Length)
                        |> Async.AwaitTask
                        |> Async.Ignore
                    let! res = client.ReceiveAsync() |> Async.AwaitTask
                    let msg = Deserialize<ServerMessage> res.Buffer
                    return msg
                }
                |> Async.RunSynchronously
            match msg with
            | ConnectionConfirmation(id, pos, state) -> 
                printfn "Successfully connected to the server, assigned id %s" 
                    (id.ToString())
                printfn "Assigned spawn at position (%i, %i)" (fst pos) 
                    (snd pos)
                Success(client, id, pos, state)
            | _ -> Failure
        with exn -> 
            client.Close()
            Failure
    
    // Simple function to create our form that we'll use
    let CreateForm() = 
        let form = new Form()
        form.Width <- 600
        form.Height <- 600
        form
    
    type FormsDelegate = delegate of unit -> unit
    
    // We add a new label to our form, the Name (or key) is the id of the player and the content is the username
    let AddPlayer (form : Form) (id : Guid) name (x, y) = 
        let AddLabel() = 
            let label = new Label()
            label.Name <- id.ToString()
            label.Text <- name
            label.Location <- Point(x, y)
            form.Controls.Add(label)
        if form.InvokeRequired then 
            let cb = FormsDelegate(AddLabel)
            form.Invoke(cb) |> ignore
        else AddLabel()
    
    // Removes the specified id from the game
    let RemovePlayer (form : Form) (id : Guid) = 
        let Remove() = form.Controls.RemoveByKey(id.ToString())
        if form.InvokeRequired then 
            let cb = FormsDelegate(Remove)
            form.Invoke(cb) |> ignore
        else Remove()
    
    // Handles the moving of a local player as it responds to local key presses
    let MovePlayer (client : UdpClient) (form : Form) state (id : Guid) 
        direction = 
        let dx, dy = 
            match direction with
            | Up -> (0, -1)
            | Down -> (0, 1)
            | Left -> (-1, 0)
            | Right -> (1, 0)
        
        let control = form.Controls.[id.ToString()]
        let loc = control.Location
        let msg = UnitCommand(Movement(direction)) |> Serialize
        client.Send(msg, msg.Length) |> ignore
        let Move() = control.Location <- Point(loc.X + dx, loc.Y + dy)
        if form.InvokeRequired then 
            let cb = FormsDelegate(Move)
            form.Invoke(cb) |> ignore
        Move()
        let user, other = 
            state |> List.partition (fun t -> 
                         match t with
                         | Player.Local _ -> true
                         | _ -> false)
        
        let user = user.Head
        
        let user = 
            match user with
            | Player.Local(client, history) -> 
                let px, py = client.Position
                Player.Local
                    ({ client with Position = (px + dx, py + dy) }, 
                     (dx, dy) :: history)
            | _ -> 
                failwith 
                    "A fatal error has occurred, we're trying to move a remote player and that's just not possible"
        user :: other
    
    let HandleClientJoin (form : Form) worldState (id : Guid) username (x, y) = 
        let client = 
            ({ Id = id
               Position = (x, y) } : Client)
            |> Player.Remote
        AddPlayer form id username (x, y)
        client :: worldState
    
    let HandleClientLeave (form : Form) worldState (id : Guid) = 
        let client = 
            worldState |> List.filter (fun t -> 
                              match t with
                              | Player.Remote p -> 
                                  if p.Id = id then false
                                  else true
                              | _ -> true)
        RemovePlayer form id
        client
    
    let MoveRemote (form : Form) (id : Guid) (x, y) = 
        let Move() = 
            let control = form.Controls.Find(id.ToString(), false).[0]
            control.Location <- Point(x, y)
        if form.InvokeRequired then 
            let cb = FormsDelegate(Move)
            form.Invoke(cb) |> ignore
        else Move()
    
    let HandleWorldUpdate (form : Form) worldState (update : Client list) = 
        let local = 
            worldState |> List.sortBy (fun t -> 
                              match t with
                              | Player.Local(client, history) -> client.Id
                              | Player.Remote(client) -> client.Id)
        
        let remote = update |> List.sortBy (fun t -> t.Id)
        
        let ApplyHistory (x, y) prevXy = 
            if prevXy = [] then (x, y)
            else 
                let dx, dy = 
                    prevXy 
                    |> List.reduce (fun (sx, sy) (x, y) -> sx + x, sy + y)
                (x + dx, y + dy)
        List.map2 (fun local (remote : Client) -> 
            match local with
            | Player.Local(client, history) -> 
                let newPos = ApplyHistory remote.Position history
                MoveRemote form client.Id newPos
                Player.Local({ client with Position = newPos }, [])
            | Player.Remote(client) -> 
                MoveRemote form client.Id remote.Position
                Player.Remote({ client with Position = remote.Position })) local 
            remote
    
    let HandleMessage (form : Form) state msg = 
        match msg with
        | ClientJoin(un, id, pos) -> HandleClientJoin form state id un pos
        | ClientLeft(id) -> HandleClientLeave form state id
        | WorldUpdate(clients) -> HandleWorldUpdate form state clients
        | _ -> state
    
    let RunGame (client : UdpClient) state (id : Guid) username (x, y) = 
        let form = CreateForm()
        AddPlayer form id username (x, y)
        state |> List.iter (fun t -> AddPlayer form t.Id t.Username t.Position)
        let others = 
            state |> List.map (fun t -> 
                         Player.Remote({ Id = t.Id
                                         Position = t.Position }))
        
        let keyObs = 
            form.KeyPress
            |> Observable.map (fun t -> 
                   match t.KeyChar with
                   | 'w' -> Up
                   | 's' -> Down
                   | 'a' -> Left
                   | 'd' -> Right
                   // A better approach here is to return Some(Command) or None but this is just an example
                   | _ -> failwith "Invalid key press")
            |> Observable.map (fun t -> Local(t))
        
        let remoteObs = 
            UdpClientToObservable client |> Observable.map (fun t -> Remote(t))
        
        let merged = 
            Observable.merge keyObs remoteObs 
            |> Observable.scan (fun state t -> 
                   match t with
                   | Local(t) -> MovePlayer client form state id t
                   | Remote(t) -> 
                       let msg = Deserialize<ServerMessage> t.Buffer
                       HandleMessage form state msg) 
                   (Player.Local({ Id = id
                                   Position = (x, y) }, []) :: others)
        merged |> Observable.add (fun t -> t |> ignore)
        Application.Run(form)
    
    [<EntryPoint>]
    let main argv = 
        System.Threading.Thread.Sleep(1000)
        let parser = UnionArgParser<Arguments>()
        let results = parser.Parse(argv)
        let ip : string = results.GetResult <@ IpAddress @>
        let port : int = results.GetResult <@ Port @>
        let localPort : int = results.GetResult <@ LocalPort @>
        let username : string = results.GetResult <@ Username @>
        let ip = IPEndPoint(IPAddress.Parse(ip), port)
        let status = PerformHandshake localPort ip username
        match status with
        | Success(client, id, pos, state) -> 
            RunGame client state id username pos
        | Failure -> 
            printfn 
                "Unable to connect to the server. Ensure the server and your internet connection are not at fault."
        Console.ReadLine() |> ignore
        0