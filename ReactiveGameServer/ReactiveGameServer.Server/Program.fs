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
    open ReactiveGameServer.Shared.Network

    let StartListening (port:int) =
        let client = new UdpClient(port)
        let obs = UdpClientToObservable client
        client, obs

    [<EntryPoint>]
    let main argv = 
        
        let parser = UnionArgParser<Arguments>()
        let results = parser.Parse(argv)
        let port : int = results.GetResult <@ Port @>
        let client, observable = StartListening port
        Console.ReadLine () |> ignore
        0
