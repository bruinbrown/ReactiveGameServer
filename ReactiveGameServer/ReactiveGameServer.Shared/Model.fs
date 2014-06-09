namespace ReactiveGameServer.Shared.Model

open System

type Direction =
    | Up
    | Down
    | Left
    | Right

// Extend this if your users can change key mappings so it could end up being
// | Move of Direction
// | Shoot of Target
// | Jump
type UnitCommand =
    | Movement of Direction

// You may want more messages here if you have more client options
// These are the messages that a client can send to the server
type ClientMessage =
    | Connect of string
    | Disconnect
    | UnitCommand of UnitCommand

// Extend this if you want players to have more state e.g. shooting, jumping
type Client = {
    Id : Guid
    Position : (int * int)
}

type PlayerInfo = {
    Id : Guid
    Position : (int * int)
    Username : string
}

// We need to handle the case in which a player gets moved from our original position,
// We'll maintain a list of Player on the client in which we have a single local player and multiple remote players
// Every time we receive a server update we fold down our (int * int) list and then apply that to our receivved position
type Player =
    | Local of Client * (int * int) list
    | Remote of Client

// Extend this if you want to support things like kicking players for inactivity etc
// These are the messages a server can send to the client
type ServerMessage =
    | ConnectionConfirmation of Guid * (int * int) * PlayerInfo list
    | WorldUpdate of Client list
    | ClientJoin of string * Guid * (int * int)
    | ClientLeft of Guid