﻿namespace Model

open System

// Extend this if your users can change key mappings so it could end up being
// | Move of Direction
// | Shoot of Target
// | Jump
type UnitCommand =
    | KeyPress of char

// You may want more messages here if you have more client options
type ClientMessage =
    | Connect of string
    | Disconnect
    | UnitCommand of UnitCommand

// Extend this if you want players to have more state e.g. shooting, jumping
type Client = {
    Id : Guid
    Position : (int * int)
}

// Extend this if you want to support things like kicking players for inactivity etc
type ServerMessage =
    | ConnectionConfirmation of Guid * (int * int)
    | WorldUpdate of Client list
    | ClientJoin of string * Guid
    | ClientLeft of Guid