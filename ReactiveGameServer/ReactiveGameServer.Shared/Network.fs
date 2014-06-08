namespace ReactiveGameServer.Shared

module Network =

    open System.Net.Sockets
    open System.Reactive
    open System.Reactive.Linq
    open System.Reactive.Threading.Tasks

    let UdpClientToObservable (client:UdpClient) =
        Observable.Defer(fun () -> client.ReceiveAsync().ToObservable()).Repeat()