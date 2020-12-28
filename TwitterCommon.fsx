#r "nuget: Akka"
#r "nuget: Akka.FSharp"
#r "nuget: Suave"

open System
open Akka.Actor
open Akka.FSharp
open System.Diagnostics
open System.Collections.Generic
open Suave.Sockets
open Suave.Sockets.Control
open Suave.WebSocket
open Suave.Utils;


let system = System.create "FSharp" (Configuration.load())

type DummyMessage =
   | Check
   | Update of int

type EngineMessage = 
    | Initialize 
    | Register of WebSocket*string
    | Login of WebSocket*string
    | Logout of WebSocket*string
    | QueryHashtags of WebSocket*string*string
    | QueryMentions of WebSocket*string*string
    | Tweet of WebSocket*string*string*Dictionary<string, WebSocket> 
    | Subscribe of WebSocket*string*string list
    | Unsubscribe of WebSocket*string*string


type SimulatorMessage = 
    | Init
    | StartSimulation
    | StartInteractive

let v = new Dictionary<String, int>()

