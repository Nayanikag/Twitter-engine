#r "nuget: Akka"
#r "nuget: Akka.Fsharp"
#r "nuget: Akka.Remote"
#r "nuget: Suave"
#r "nuget: Newtonsoft.Json"
#r "nuget: System.Net.Http"
#r "nuget: FSharp.Data"
#r "nuget: FSharp.Json"
#r "nuget: FSharpx.Collections"
#load "Engine.fsx"

open Suave
open Suave.Http
open Suave.Operators
open Suave.Filters
open Suave.Successful
open Suave.Files
open Suave.RequestErrors
open Suave.Logging
open Suave.Utils
open FSharp.Data
open Newtonsoft
open System.Collections.Generic
open Akka.Actor
open Akka.FSharp
open System
open System.Net

open Suave.Sockets
open Suave.Sockets.Control
open Suave.WebSocket

 
let myCfg =
  { defaultConfig with
      bindings = [ HttpBinding.createSimple HTTP "127.0.0.1" 8082 ]
    }

let mutable global_websocket_map = new Dictionary<string, WebSocket>()
let subscribers = ["imran"; "mowli"]
let engine = spawn TwitterCommon.system "Engine" Engine.EngineActor
let engineRef = select "akka://FSharp/user/Engine" TwitterCommon.system

System.Console.WriteLine("Configured")
let webRoot = @"\wwwroot"
type TextMessage={From:string}
let ws (webSocket : WebSocket) (context: HttpContext) =
  socket {
    let mutable loop = true

    while loop do
      let! msg = webSocket.read()

      match msg with
      | (Text, data, true) ->
        let str = UTF8.toString data
        let response = sprintf "response to %s" str
        let mutable res = ""
        let inp = Json.JsonConvert.DeserializeObject<JsonApi.Request>(str)
        if inp.func = "Register" then
            if global_websocket_map.ContainsKey(inp.username) = false then
                    global_websocket_map.Add(inp.username, webSocket)
            engineRef  <! TwitterCommon.EngineMessage.Initialize
            engineRef  <! TwitterCommon.EngineMessage.Register(webSocket, inp.username)
        if inp.func = "Login" then
            if global_websocket_map.ContainsKey(inp.username) = false then
                    global_websocket_map.Add(inp.username, webSocket)
            engineRef  <! TwitterCommon.EngineMessage.Initialize
            engineRef  <! TwitterCommon.EngineMessage.Login(webSocket, inp.username)

        if inp.func = "Tweet" then
            engineRef  <! TwitterCommon.EngineMessage.Tweet(webSocket, inp.username, inp.input, global_websocket_map)
        if inp.func = "Hashtag" then
            engineRef  <! TwitterCommon.EngineMessage.QueryHashtags(webSocket, inp.username, inp.input)

        if inp.func = "Mention" then
            engineRef  <! TwitterCommon.EngineMessage.QueryMentions(webSocket, inp.username, inp.input)

        if inp.func = "Subscribe" then
            engineRef  <! TwitterCommon.EngineMessage.Subscribe(webSocket, inp.username, [inp.input])

        if inp.func = "Unsubscribe" then

            engineRef  <! TwitterCommon.EngineMessage.Unsubscribe(webSocket, inp.username, inp.input)

        if inp.func = "Logout" then
            engineRef  <! TwitterCommon.EngineMessage.Logout(webSocket, inp.username)
            if global_websocket_map.ContainsKey(inp.username) = true then
                for pair in global_websocket_map do
                    if pair.Value = webSocket then
                        global_websocket_map.Remove(pair.Key)
                let emptyResponse = [||] |> ByteSegment
                do! webSocket.send Close emptyResponse true
            
            

      | (Close, _, _) ->
        let emptyResponse = [||] |> ByteSegment
        for pair in global_websocket_map do
            if pair.Value = webSocket then
                global_websocket_map.Remove(pair.Key)
        do! webSocket.send Close emptyResponse true
        
        loop <- false

      | _ -> ()
    }

let app : WebPart = 
  choose [
    path "/websocket" >=> handShake ws
    GET >=> path "/index" >=> Files.file "./index.html"
    NOT_FOUND "Found no handlers." ]                                
startWebServer myCfg app