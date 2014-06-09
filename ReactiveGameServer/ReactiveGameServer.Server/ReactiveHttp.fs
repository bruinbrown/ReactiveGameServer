namespace ReactiveGameServer

open System
open System.IO
open System.Net
open System.Reactive.Linq
open System.Reactive.Threading.Tasks
open System.Collections.Generic
open ReactiveGameServer.Shared.Model

type ConnectedClient = 
    { Id : Guid
      Username : string
      LastReceived : DateTime
      Position : int * int
      Connection : IPEndPoint }

module ReactiveHttp = 
    let WriteText (stream : Stream) (str : string) = 
        let bytes = Text.Encoding.UTF8.GetBytes(str)
        stream.Write(bytes, 0, bytes.Length)
    
    let CreateWebServerObservable url = 
        let listener = new HttpListener()
        listener.Prefixes.Add(url)
        listener.Start()
        let obs = 
            Observable.Defer(fun () -> listener.GetContextAsync().ToObservable())
                      .Repeat()
        listener, obs
    
    let StartWebStatusApi url (observable : IObservable<ConnectedClient list>) = 
        let listener, obs = CreateWebServerObservable url
        let mostrecent = 
            observable.Sample(TimeSpan.FromSeconds(1.0)).MostRecent([])
        obs.Zip(mostrecent, 
                fun a b -> 
                    printfn "Received request"
                    let b = 
                        b |> List.map (fun t -> 
                                 { Id = t.Id
                                   Position = t.Position } : Client)
                    
                    let res = Newtonsoft.Json.JsonConvert.SerializeObject(b)
                    a.Response.ContentLength64 <- res.Length |> int64
                    WriteText a.Response.OutputStream res).Publish().Connect()
