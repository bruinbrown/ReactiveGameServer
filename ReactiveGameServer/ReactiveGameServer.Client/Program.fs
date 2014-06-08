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
    open System.Net
    open System.Net.Sockets
    open ReactiveGameServer.Shared.Model
    open ReactiveGameServer.Shared.Network

    type ConnectionStatus =
        | Success of UdpClient * Guid * (int * int)
        | Failure

    let PerformHandshake (localPort:int) endPoint username =
        let client = new UdpClient(localPort)
        client.Connect(endPoint)
        try
            let msg = async {
                          let connection = Connect(username)
                          let msg = Serialize connection
                          do! client.SendAsync(msg, msg.Length) |> Async.AwaitTask |> Async.Ignore
                          let! res = client.ReceiveAsync () |> Async.AwaitTask
                          let msg = Deserialize<ServerMessage> res.Buffer
                          return msg
                      } |> Async.RunSynchronously
            match msg with
            | ConnectionConfirmation(id, pos) -> printfn "Successfully connected to the server, assigned id %s" (id.ToString())
                                                 Success(client, id, pos)
            | _ -> Failure
        with
        | exn -> client.Close ()
                 Failure

    [<EntryPoint>]
    let main argv = 
        System.Threading.Thread.Sleep(1000)
        let parser = UnionArgParser<Arguments> ()
        let results = parser.Parse(argv)
        let ip : string = results.GetResult <@ IpAddress @>
        let port : int = results.GetResult <@ Port @>
        let localPort : int = results.GetResult <@ LocalPort @>
        let username : string = results.GetResult <@ Username @>
        let ip = IPEndPoint(IPAddress.Parse(ip), port)
        let status = PerformHandshake localPort ip username
        match status with
        | Success (client, id, pos) -> ()
        | Failure -> printfn "Unable to connect to the server. Ensure the server and your internet connection are not at fault."
        Console.ReadLine() |> ignore
        0
