namespace ReactiveGameServer.Shared

module Network = 
    open System.Net.Sockets
    open System.Reactive
    open System.Reactive.Linq
    open System.Reactive.Threading.Tasks
    
    let UdpClientToObservable(client : UdpClient) = 
        Observable.Defer(fun () -> client.ReceiveAsync().ToObservable())
                  .Repeat()
    
    open System.IO
    open System.Runtime.Serialization.Formatters.Binary
    
    let private bf = new BinaryFormatter()
    
    let Serialize msg = 
        let ms = new MemoryStream()
        bf.Serialize(ms, msg)
        ms.ToArray()
    
    let Deserialize<'a> bytes = 
        let ms = new MemoryStream()
        ms.Write(bytes, 0, bytes.Length)
        ms.Position <- 0L
        bf.Deserialize(ms) :?> 'a
