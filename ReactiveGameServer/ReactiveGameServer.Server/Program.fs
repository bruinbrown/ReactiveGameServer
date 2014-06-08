namespace ReactiveGameServer.Server

open Nessos.UnionArgParser

type Arguments =
    | [<Mandatory>] Port of int
    interface IArgParserTemplate with
        member this.Usage =
            match this with
            | Port _ -> "Port on which to listen"

module Core =

    open System
    open System.Net
    open System.Net.Sockets
    open ReactiveGameServer.Shared
    open ReactiveGameServer.Shared.Model
    open ReactiveGameServer.Shared.Network

    type ConnectedClient = {
        Id : Guid
        Username : string
        LastReceived : DateTime
        Position : int * int
        Connection : IPEndPoint
    }

    let StartListening (port:int) =
        let client = new UdpClient(port)
        let obs = UdpClientToObservable client
        let o = obs |> Observable.map (fun t -> t.RemoteEndPoint, (Deserialize<ClientMessage> t.Buffer))
        client, o
    let private random = Random()

    let ComputeSpawnPosition worldState =
        // For now this function just returns a new location but an actual game probably will instead spawn a player in the most suitable position
        random.Next(600), random.Next(600)

    let HandleConnectionRequest (client:UdpClient) worldState connection un =
        let id = Guid.NewGuid()
        let pos = ComputeSpawnPosition worldState
        let newPlayer = { Id = id; Username = un; LastReceived = DateTime.UtcNow; Position = pos; Connection = connection }
        printfn "Received a connection request from user %s at IP %s, assigning ID %s" un (connection.ToString()) (id.ToString())
        let clientMsg = ConnectionConfirmation(id, pos) |> Serialize
        client.SendAsync(clientMsg, clientMsg.Length, connection) |> Async.AwaitTask |> Async.RunSynchronously |> ignore
        let msg = ClientJoin(un,id) |> Serialize
        worldState
        |> List.iter (fun t -> client.SendAsync(msg, msg.Length) |> Async.AwaitTask |> Async.RunSynchronously |> ignore)
        newPlayer::worldState

    let HandleDisconnectionRequest (client:UdpClient) worldState connection =
        let disconnecting, others = worldState
                                    |> List.partition(fun t -> if t.Connection = connection then true else false)
        printfn "Player at %s disconnecting" (connection.ToString())
        let msg = ClientLeft(disconnecting.Head.Id) |> Serialize
        others
        |> List.iter (fun t -> client.SendAsync(msg, msg.Length) |> Async.AwaitTask |> Async.RunSynchronously |> ignore)
        others

    let HandleMessage (client:UdpClient) worldState (connection, msg) =
        match msg with
        | Connect(un) -> HandleConnectionRequest client worldState connection un
        | Disconnect -> HandleDisconnectionRequest client worldState connection
        | _ -> worldState

    [<EntryPoint>]
    let main argv = 
        let parser = UnionArgParser<Arguments>()
        let results = parser.Parse(argv)
        let port : int = results.GetResult <@ Port @>
        let client, observable = StartListening port
        let state = observable
                    |> Observable.scan (fun s t -> printfn "%A" t
                                                   HandleMessage client s t) []
        // For now we need to add this throwaway observer otherwise nothing will run, later on though we can change this slightly 
        state.Add(fun t -> printfn "Completed message handler")
        Console.ReadLine () |> ignore
        0
