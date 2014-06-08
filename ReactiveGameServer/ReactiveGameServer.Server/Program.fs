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
        client, obs |> Observable.map (fun t -> t.RemoteEndPoint, (Deserialize<ClientMessage> t.Buffer))


    let private random = Random()

    let ComputeSpawnPosition worldState =
        // For now this function just returns a new location but an actual game probably will instead spawn a player in the most suitable position
        random.Next(600), random.Next(600)

    let HandleConnectionRequest worldState connection un =
        let id = Guid.NewGuid()
        let pos = ComputeSpawnPosition worldState
        let newPlayer = { Id = id; Username = un; LastReceived = DateTime.UtcNow; Position = pos; Connection = connection }
        newPlayer::worldState

    let HandleDisconnectionRequest worldState connection =
        worldState
        |> List.filter (fun t -> t.Connection <> connection)

    let HandleMessage (client:UdpClient) worldState (connection, msg) =
        match msg with
        | Connect(un) -> HandleConnectionRequest worldState connection un
        | Disconnect -> HandleDisconnectionRequest worldState connection
        | _ -> worldState

    [<EntryPoint>]
    let main argv = 
        let parser = UnionArgParser<Arguments>()
        let results = parser.Parse(argv)
        let port : int = results.GetResult <@ Port @>
        let client, observable = StartListening port
        let state = observable
                    |> Observable.scan (HandleMessage client) [] 
        Console.ReadLine () |> ignore
        0
