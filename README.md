ReactiveGameServer
==================

If you want to have multiple players playing your game at once then the chances are you'll have had to look at a load of different ways of message passing between clients and server. This project aims to provide a simple example of creating a UDP socket based game server based around the ideas surrounding reactive programming. The client exposes a single observable which is used to enable applications to subscribe to any changes which happen with the other clients which are connected to the server.  
  
This project is designed to serve as an example and is not something which can just be dropped into a project. It's also lacking a lot of features like:
  - Notifying matchmaking services of server availability
  - Server state HTTP API (think something like Halo's Presence feature)
  - Safer code (it takes some shortcuts at the minute)
  - A decent game engine to combine it with (something with features like those in the elm language)
  - Lag prediction, players can jump around a lot in the case of large amounts of lag
  - Player timeouts for connections which haven't moved in a time period or those who haven't sent any messages and so have likely disconnected
