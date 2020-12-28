#r "nuget: System.Data.SqlClient"
#r "nuget: Akka"
#r "nuget: Akka.Fsharp"
#r "nuget: Akka.Remote"
#r "nuget: FSharpx.Collections"
#load "TwitterCommon.fsx"
#load "JsonApi.fsx"
#r "nuget: FSharp.Json"
#r "nuget: Newtonsoft.Json"
#r "nuget: Suave"

open System
open Akka.Actor
open Akka.FSharp
open System.Diagnostics
open System.Data.SqlClient
open System.Data
open System.Collections.Generic
open System.Text.RegularExpressions
open System.IO
//open FSharp.Json
open Suave.Sockets
open Suave.Sockets.Control
open Suave.WebSocket
open Suave.Utils;
open Newtonsoft


// User Table ...
let userTable = new DataTable("Users")
userTable.Columns.Add(new DataColumn("UserName"))
userTable.Columns.Add(new DataColumn("Status"))
userTable.Columns.Add(new DataColumn("Subscribers"))
userTable.Columns.Add(new DataColumn("Feed"))
userTable.Columns.Add(new DataColumn("Port"))

// HashTag Table ...
let hashTagTable = new DataTable("HashTag")
hashTagTable.Columns.Add(new DataColumn("HashTag"))
hashTagTable.Columns.Add(new DataColumn("Tweet",typeof<string list>))

// Mention table ...
let mentionTable = new DataTable("Mention")
mentionTable.Columns.Add(new DataColumn("Mention"))
mentionTable.Columns.Add(new DataColumn("Tweet"))

// Counter table ...
let counterTable = new DataTable("Counter")
counterTable.Columns.Add(new DataColumn("Entity"))
counterTable.Columns.Add(new DataColumn("Count",typeof<int>))
counterTable.Rows.Add("tweets", 0)
counterTable.Rows.Add("total_users", 0)
counterTable.Rows.Add("online_users", 0)
counterTable.Rows.Add("offline_users", 0)
let mutable global_user_list = []
let mutable global_user_map = new Dictionary<string, int>()
let mutable modeToPer = ""
let mutable numofClients = 0
let mutable numofActors = 10

let rec removeElement n lst = 
    match lst with
    | h::tl when h = n -> tl
    | h::tl -> h :: (removeElement n tl)
    | []    -> []


let EngineActor (mailbox: Actor<_>)= 
    let rec loop (numTweets:int, total_users:int, online_users:int, offline_users:int, displayCounter:int, totalNumRequests:int) = actor {
        let! message  =  mailbox.Receive ()
        let sender = mailbox.Sender()
        let updateTotalRequests = totalNumRequests + 10
        //System.Console.WriteLine(modeToPer)
            
          // check for end condition 
        if totalNumRequests >= (51000) then
            let strtoID = "akka://FSharp/user/DummyAct"
            let dumrefer = select strtoID TwitterCommon.system
            dumrefer <! TwitterCommon.DummyMessage.Update totalNumRequests

        let populateGlobalUserList n = 
            global_user_list <- List.append ["Dishe"] global_user_list
            global_user_list <- List.append ["Neyanika"] global_user_list
            global_user_list <- List.append ["Steve"] global_user_list
            global_user_list <- List.append ["Rahul"] global_user_list
            global_user_list <- List.append ["Sam"] global_user_list
            global_user_list <- List.append ["Troy"] global_user_list
            global_user_list <- List.append ["James"] global_user_list
            global_user_list <- List.append ["Arpita"] global_user_list
            global_user_list <- List.append ["Emma"] global_user_list
            global_user_list <- List.append ["Rachel"] global_user_list


        let checkIfUserExists(username) = 
            let foundRows = userTable.Select("UserName = '" + username + "'")
            if foundRows.Length <> 0 then
                true
            else
                false     

        let send_feed(websocket : WebSocket, username, client) = 

            let foundRows = userTable.Select("UserName = '" + username + "'")
            let mutable feed_l = []

            let feeds = foundRows.[0].["Feed"]
            let feed_str = feeds.ToString()
            let trim_char = ['[';']']
            feed_str = feed_str.Substring(1, feed_str.Length - 2)
            //System.Console.WriteLine(foundRows.[0].["Feed"])

            if feed_str <> "[[]]" then

                let feed_s = feed_str.Substring(1, feed_str.Length - 2)
                let mutable feed_list = []
                let eachTweetArray = feed_s.Split ';'
                let mutable i = 0
                while i < (eachTweetArray.Length) do
                    let username = ((eachTweetArray.[i]).Split '[').[1]
                    let tweet = ((eachTweetArray.[i+1]).Split ']').[0]

                    let lst = List.append [username.Trim()] [tweet.Trim()]
                    feed_list <- List.append feed_list [lst]
                    i <- i + 2
                let s=socket {
                    let data = {JsonApi.Response.username = username |> string; JsonApi.Response.func = "Feed"; JsonApi.Response.success = "True"; 
                                    JsonApi.Response.tweet = ""; JsonApi.Response.hashtag= ""; JsonApi.Response.mention = ""; JsonApi.Response.subscribe = ""; 
                                    JsonApi.Response.unsubscribe = ""; JsonApi.Response.results = []; JsonApi.Response.feed = feed_list;JsonApi.Response.error = "" }
                    let res = Json.JsonConvert.SerializeObject(data)
                    let byteResponse =
                      res
                      |> System.Text.Encoding.ASCII.GetBytes
                      |> ByteSegment
                    do! websocket.send Text byteResponse true
                }
                Async.StartAsTask s |> ignore
                foundRows.[0].["Feed"] <- [[]]


        let send_tweet (to_username, global_websocket_map:Dictionary<string, WebSocket> , from:string, tweet:string) =
            if global_websocket_map.ContainsKey(to_username) = true then
                let s=socket {
                    let lst = List.append [from] [tweet]
                    let data = {JsonApi.Response.username = to_username |> string; JsonApi.Response.func = "Feed"; JsonApi.Response.success = "True"; 
                                    JsonApi.Response.tweet = ""; JsonApi.Response.hashtag= ""; JsonApi.Response.mention = ""; JsonApi.Response.subscribe = ""; 
                                    JsonApi.Response.unsubscribe = ""; JsonApi.Response.results = []; JsonApi.Response.feed = [lst];JsonApi.Response.error = ""}
                    let res = Json.JsonConvert.SerializeObject(data)
                    let byteResponse =
                      res
                      |> System.Text.Encoding.ASCII.GetBytes
                      |> ByteSegment
                    do! global_websocket_map.[to_username].send Text byteResponse true
                }
                Async.StartAsTask s |> ignore
                    //TO DO add send data to websocket
                    //clientRef <! TwitterCommon.ClientMessage.Feed (to_username, feed_list)
            else
                let foundRows = userTable.Select("UserName = '" + to_username + "'")
                if foundRows.Length <> 0 then
                    let feeds = foundRows.[0].["Feed"]
                    let mutable feed_str = feeds.ToString()
                    let trim_char = ['[';']']
                    feed_str <- feed_str.Substring(1, feed_str.Length - 2)
                    if feed_str <> "[]" then
                        //System.Console.WriteLine("found " + feed_str)
                        let lst1 = List.append [from] [tweet]
                        let mutable feed_list = [lst1]
                        let eachTweetArray = feed_str.Split ';'
                        let mutable i = 0
                        while i < (eachTweetArray.Length) do
                            let username = ((eachTweetArray.[i]).Split '[').[1]
                            let tweet = ((eachTweetArray.[i+1]).Split ']').[0]

                            let lst = List.append [username.Trim()] [tweet.Trim()]
                            feed_list <- List.append feed_list [lst]
                            i <- i + 2
                        foundRows.[0].["Feed"] <- feed_list
                    else
                        let lst = List.append [from] [tweet]
                        //System.Console.WriteLine(foundRows.Length)

                        foundRows.[0].["Feed"] <- [lst]
                //else
                //  System.Console.WriLine(to_username)


        let zipfConstant (usercount:int) = 
            let users = [for i in 1 .. usercount -> 1.0/float(i)]
            let mutable sum = 0.0
            for i in users do 
                sum <- sum + i
            sum <- sum ** -1.0
            sum

        let zipfProb (constant: float, user, usercount) = 
            let prob = ceil((constant/float(user))*float(usercount))
            int(prob)

        let getRandom (inpLst : string list)=
            let r = System.Random()
            let ind = r.Next(inpLst.Length);
            let num = inpLst.[ind]
            num

        let getSubscribers (subcount, tempsubscribersList: string list) = 
            let mutable tempsubscribers = []
            for i in [1 .. subcount] do 
                let n = getRandom tempsubscribersList
                tempsubscribers <- List.append [n] tempsubscribers
            tempsubscribers

        match message with 
        | TwitterCommon.EngineMessage.Initialize -> 
            numofClients <- 10
            let mutable socketid = 0
            // will socket id be same as clientid 
            let constant = zipfConstant numofClients
            if global_user_list = [] then
                populateGlobalUserList 10

            let mutable userCounter = 0
            for i in [0 .. numofClients-1] do
                let username = global_user_list.[i]
                let subscriberCount = zipfProb (constant, userCounter+1, numofClients)
                let tempsubscribersList = removeElement username global_user_list 
                //let subscribers = getSubscribers (subscriberCount, tempsubscribersList)
                if checkIfUserExists(username) = false then
                    userTable.Rows.Add(username, false, [], [[]], 0)
                    File.AppendAllText("Register.txt", "User - " + username + " successfully registered & assigned 
                    default subscribers from zipf distribution" + "\n")                  
            return! loop(numTweets, total_users + 1, online_users + 1, offline_users, displayCounter, totalNumRequests+1)

        | TwitterCommon.EngineMessage.Register (ws, username) -> 
            // register a single user 
            numofClients <- numofClients + 1
            let actorId = (numofClients % numofActors)
            global_user_list  <- List.append global_user_list [username]        
            if checkIfUserExists(username) = false then
                userTable.Rows.Add(username, true, [], [[]], 0)
                File.AppendAllText("Register.txt", "User - " + username + " successfully registered & assigned 
                default subscribers from zipf distribution" + "\n")
                // send data 
                let s=socket {
                    let data = {JsonApi.Response.username = username |> string; JsonApi.Response.func = "Register"; JsonApi.Response.success = "True"; 
                                    JsonApi.Response.tweet = ""; JsonApi.Response.hashtag= ""; JsonApi.Response.mention = ""; JsonApi.Response.subscribe = ""; 
                                    JsonApi.Response.unsubscribe = ""; JsonApi.Response.results = []; JsonApi.Response.feed = [];JsonApi.Response.error = ""}
                    let res = Json.JsonConvert.SerializeObject(data)
                    let byteResponse =
                          res
                            |> System.Text.Encoding.ASCII.GetBytes
                            |> ByteSegment
                    do! ws.send Text byteResponse true 
                }               
                Async.StartAsTask s |> ignore
            else 
                // send error response
                let s=socket {
                    let data = {JsonApi.Response.username = username |> string; JsonApi.Response.func = "Register"; JsonApi.Response.success = "False"; 
                                    JsonApi.Response.tweet = ""; JsonApi.Response.hashtag= ""; JsonApi.Response.mention = ""; JsonApi.Response.subscribe = ""; 
                                    JsonApi.Response.unsubscribe = ""; JsonApi.Response.results = []; JsonApi.Response.feed = []; JsonApi.Response.error = "user could not be registered"}
                    let res = Json.JsonConvert.SerializeObject(data)
                    let byteResponse =
                          res
                            |> System.Text.Encoding.ASCII.GetBytes
                            |> ByteSegment
                    do! ws.send Text byteResponse true 
                }
                Async.StartAsTask s |> ignore
            return! loop(numTweets, total_users + 1, online_users + 1, offline_users, displayCounter, totalNumRequests+1)

        | TwitterCommon.EngineMessage.Login (ws, username) ->
            let  mutable loggedInUser = 0
            let mutable updated_offline_users = offline_users
            if checkIfUserExists(username) = true then
                let foundRows = userTable.Select("UserName = '" + username + "'")
                if foundRows.[0].["Status"].ToString() = "False" then
                    foundRows.[0].["Status"] <- true
                    loggedInUser <- loggedInUser + 1
                    updated_offline_users = updated_offline_users - 1

                    File.AppendAllText("Login.txt", "User - " + username  + ": User successfully logged in" + "\n")  
                let s=socket {
                    let data = {JsonApi.Response.username = username |> string; JsonApi.Response.func = "Login"; JsonApi.Response.success = "True"; 
                                    JsonApi.Response.tweet = ""; JsonApi.Response.hashtag= ""; JsonApi.Response.mention = ""; JsonApi.Response.subscribe = ""; 
                                    JsonApi.Response.unsubscribe = ""; JsonApi.Response.results = []; JsonApi.Response.feed = [];JsonApi.Response.error = "" }
                    let res = Json.JsonConvert.SerializeObject(data)
                    let byteResponse =
                          res
                            |> System.Text.Encoding.ASCII.GetBytes
                            |> ByteSegment
                    do! ws.send Text byteResponse true
                    send_feed(ws,username, 0)
                }
                Async.StartAsTask s |> ignore                                    
            else 
                let s=socket {
                    let data = {JsonApi.Response.username = username |> string; JsonApi.Response.func = "Login"; JsonApi.Response.success = "False"; 
                                    JsonApi.Response.tweet = ""; JsonApi.Response.hashtag= ""; JsonApi.Response.mention = ""; JsonApi.Response.subscribe = ""; 
                                    JsonApi.Response.unsubscribe = ""; JsonApi.Response.results = []; JsonApi.Response.feed = []; JsonApi.Response.error = "error while logging in, user does not exist."}
                    let res = Json.JsonConvert.SerializeObject(data)
                    let byteResponse =
                          res
                            |> System.Text.Encoding.ASCII.GetBytes
                            |> ByteSegment
                    do! ws.send Text byteResponse true 
                }
                Async.StartAsTask s |> ignore                
            File.AppendAllText("Login.txt",  "\n")
            return! loop(numTweets, total_users, online_users + loggedInUser, updated_offline_users, displayCounter, updateTotalRequests)
            

        | TwitterCommon.EngineMessage.Logout(ws, username) ->
            let mutable updated_users = 0
            if checkIfUserExists(username) = true then
                let foundRows = userTable.Select("UserName = '" + username + "'")
                if foundRows.[0].["Status"].ToString() = "True" then
                    foundRows.[0].["Status"] <-false
                    updated_users <- updated_users + 1                
            return! loop(numTweets, total_users, online_users - updated_users, offline_users + updated_users,displayCounter, updateTotalRequests)


        | TwitterCommon.EngineMessage.QueryHashtags(ws, username, hashtag) -> 
            // get tweets
            // for all tweet, send tweets, hashtag & username to client
            let foundRows = hashTagTable.Select("HashTag = '" + hashtag + "'")
            if foundRows.Length <> 0 then
                let tweet_str = foundRows.[0].["Tweet"].ToString()
                let tweets_chunk = (((tweet_str.Split '[').[1].Split ']').[0]).Split ';'
                let mutable tweet_list = []
                for tw in tweets_chunk do                  
                    tweet_list <- List.append tweet_list [tw.Trim()]
                let s=socket {
                    let data = {JsonApi.Response.username = username |> string; JsonApi.Response.func = "Hashtag"; JsonApi.Response.success = "True"; 
                                    JsonApi.Response.tweet = ""; JsonApi.Response.hashtag= hashtag; JsonApi.Response.mention = ""; JsonApi.Response.subscribe = ""; 
                                    JsonApi.Response.unsubscribe = ""; JsonApi.Response.results = tweet_list; JsonApi.Response.feed = [];JsonApi.Response.error = "" }
                    let res = Json.JsonConvert.SerializeObject(data)
                    let byteResponse =
                          res
                            |> System.Text.Encoding.ASCII.GetBytes
                            |> ByteSegment
                    do! ws.send Text byteResponse true 
                }
                Async.StartAsTask s |> ignore                
            else
                let s=socket {
                    let data = {JsonApi.Response.username = username |> string; JsonApi.Response.func = "Hashtag"; JsonApi.Response.success = "False"; 
                                    JsonApi.Response.tweet = ""; JsonApi.Response.hashtag= hashtag; JsonApi.Response.mention = ""; JsonApi.Response.subscribe = ""; 
                                    JsonApi.Response.unsubscribe = ""; JsonApi.Response.results = []; JsonApi.Response.feed = [];JsonApi.Response.error = "error while querying for a hashtag" }
                    let res = Json.JsonConvert.SerializeObject(data)
                    let byteResponse =
                          res
                            |> System.Text.Encoding.ASCII.GetBytes
                            |> ByteSegment
                    do! ws.send Text byteResponse true 
                }
                Async.StartAsTask s |> ignore                 
            return! loop(numTweets, total_users, online_users, offline_users, displayCounter, updateTotalRequests)

        | TwitterCommon.EngineMessage.QueryMentions(ws, username, mention) -> 
            // get tweets
            // for all tweet, send tweets, hashtag & username to client
            //System.Console.WriteLine("Query for mention : ")
            //System.Console.WriteLine(mention)
            let foundRows = mentionTable.Select("Mention = '" + mention + "'")
            //System.Console.WriteLine("num rows:" + string(foundRows.Length))
            if foundRows.Length <> 0 then
                let tweet_str = foundRows.[0].["Tweet"].ToString()
                let tweets_chunk = (((tweet_str.Split '[').[1].Split ']').[0]).Split ';'
                let mutable tweet_list = []
                for tw in tweets_chunk do                  
                    tweet_list <- List.append tweet_list [tw.Trim()]
                let s=socket {
                    let data = {JsonApi.Response.username = username |> string; JsonApi.Response.func = "Mention"; JsonApi.Response.success = "True"; 
                                    JsonApi.Response.tweet = ""; JsonApi.Response.hashtag= ""; JsonApi.Response.mention = mention; JsonApi.Response.subscribe = ""; 
                                    JsonApi.Response.unsubscribe = ""; JsonApi.Response.results = tweet_list; JsonApi.Response.feed = []; JsonApi.Response.error = ""}
                    let res = Json.JsonConvert.SerializeObject(data)
                    let byteResponse =
                          res
                            |> System.Text.Encoding.ASCII.GetBytes
                            |> ByteSegment
                    do! ws.send Text byteResponse true 
                }
                Async.StartAsTask s |> ignore                  
            else
                let s=socket {
                    let data = {JsonApi.Response.username = username |> string; JsonApi.Response.func = "Mention"; JsonApi.Response.success = "False"; 
                                    JsonApi.Response.tweet = ""; JsonApi.Response.hashtag= ""; JsonApi.Response.mention = mention; JsonApi.Response.subscribe = ""; 
                                    JsonApi.Response.unsubscribe = ""; JsonApi.Response.results = []; JsonApi.Response.feed = []; JsonApi.Response.error = "error in query mention"}
                    let res = Json.JsonConvert.SerializeObject(data)
                    let byteResponse =
                          res
                            |> System.Text.Encoding.ASCII.GetBytes
                            |> ByteSegment
                    do! ws.send Text byteResponse true 
                }
                Async.StartAsTask s |> ignore                
            return! loop(numTweets, total_users, online_users, offline_users, displayCounter, updateTotalRequests)

        | TwitterCommon.EngineMessage.Tweet(websocket:WebSocket, username:string, tweet: string, global_websocket_map:Dictionary<string, WebSocket>) ->
            //change in param
            let mutable c = 0
            // Add hashtag to hashtag table
            let regexp = "#[A-Za-z0-9]*"
            let m = Regex.Matches(tweet, regexp)
            for i in m do
                if i.Success then
                    for g in i.Groups do
                        let foundRows = hashTagTable.Select("HashTag = '" + g.Value + "'")
                        if foundRows.Length <> 0 then

                            let tweet_str = foundRows.[0].["Tweet"].ToString()
                            let tweets_chunk = (((tweet_str.Split '[').[1].Split ']').[0]).Split ';'
                            let mutable tweet_list = []
                            for tw in tweets_chunk do
                               tweet_list <- List.append tweet_list [tw.Trim()]
                            foundRows.[0].["Tweet"] <- List.append tweet_list [tweet.Trim()]
                        else
                            hashTagTable.Rows.Add(g, [tweet])
            // Add mention to mention table                
            let regexp = "@[A-Za-z0-9]*"
            let m = Regex.Matches(tweet, regexp)
            for i in m do
                if i.Success then
                    for g in i.Groups do
                        let mentionedUser = g.Value.Substring(1, (g.Value).Length - 1)
                        let foundRows = mentionTable.Select("Mention = '" + mentionedUser + "'")
                        if foundRows.Length <> 0 then
                            let tweet_str = foundRows.[0].["Tweet"].ToString()
                            let tweets_chunk = (((tweet_str.Split '[').[1].Split ']').[0]).Split ';'
                            let mutable tweet_list = []
                            for tw in tweets_chunk do
                               tweet_list <- List.append tweet_list [tw.Trim()]
                            foundRows.[0].["Tweet"] <- List.append tweet_list [tweet.Trim()]
                        else
                            mentionTable.Rows.Add(mentionedUser, [tweet])                        
                        //let mentionedClientId = global_user_map.[mentionedUser]
                        // change
                        send_tweet(mentionedUser, global_websocket_map, username, tweet)

            // add feeds to Subscriber
            let foundRows = userTable.Select("UserName = '" + username + "'")
            if foundRows.Length <> 0 then
                let subscriber_str = foundRows.[0].["Subscribers"].ToString()
                if subscriber_str <> "[]" then
                    let subscriber_chunk = (((subscriber_str.Split '[').[1].Split ']').[0]).Split ';'
                    let mutable subscriber_client = 0
                    for subscriber in subscriber_chunk do
                        let updatedSubscriber = subscriber.Trim()                      
                        //subscriber_client <- global_user_map.[username]
                        //change
                        send_tweet(updatedSubscriber, global_websocket_map, username, tweet)                            
                c <- c + 1
                let s=socket {
                    let data = {JsonApi.Response.username = username |> string; JsonApi.Response.func = "Tweet"; JsonApi.Response.success = "True"; 
                                    JsonApi.Response.tweet = ""; JsonApi.Response.hashtag= ""; JsonApi.Response.mention = ""; JsonApi.Response.subscribe = ""; 
                                    JsonApi.Response.unsubscribe = ""; JsonApi.Response.results = []; JsonApi.Response.feed = []; JsonApi.Response.error = "" }
                    let res = Json.JsonConvert.SerializeObject(data)
                    let byteResponse =
                          res
                            |> System.Text.Encoding.ASCII.GetBytes
                            |> ByteSegment
                    do! websocket.send Text byteResponse true 
                }
                Async.StartAsTask s |> ignore
            return! loop(numTweets+1, total_users, online_users, offline_users, displayCounter+1, updateTotalRequests) 
                                                                                                 

        | TwitterCommon.EngineMessage.Subscribe(ws, username, follow_list) -> 
            let foundRows = userTable.Select("UserName = '" + follow_list.[0] + "'")
            if foundRows.Length <> 0 then 
                let subscriber_str = foundRows.[0].["Subscribers"].ToString()
                let subscriber_chunk = (((subscriber_str.Split '[').[1].Split ']').[0]).Split ';'
                let mutable subscriber_list = []
                if subscriber_chunk.Length > 0 then
                    for subscriber in subscriber_chunk do
                       subscriber_list <- List.append subscriber_list [subscriber]
                //System.Console.WriteLine("Before: " + foundRows.[0].["Subscribers"].ToString())
                foundRows.[0].["Subscribers"] <- List.append subscriber_list [username]
                //System.Console.WriteLine("After: " + foundRows.[0].["Subscribers"].ToString())
                let s=socket {
                    let data = {JsonApi.Response.username = username |> string; JsonApi.Response.func = "Subscribe"; JsonApi.Response.success = "True"; 
                                    JsonApi.Response.tweet = ""; JsonApi.Response.hashtag= ""; JsonApi.Response.mention = ""; JsonApi.Response.subscribe = follow_list.[0]; 
                                    JsonApi.Response.unsubscribe = ""; JsonApi.Response.results = []; JsonApi.Response.feed = [];JsonApi.Response.error = "" }
                    let res = Json.JsonConvert.SerializeObject(data)
                    let byteResponse =
                          res
                            |> System.Text.Encoding.ASCII.GetBytes
                            |> ByteSegment
                    do! ws.send Text byteResponse true 
                }
                Async.StartAsTask s |> ignore   
            else 
                let s=socket {
                    let data = {JsonApi.Response.username = username |> string; JsonApi.Response.func = "Subscribe"; JsonApi.Response.success = "False"; 
                                    JsonApi.Response.tweet = ""; JsonApi.Response.hashtag= ""; JsonApi.Response.mention = ""; JsonApi.Response.subscribe = follow_list.[0]; 
                                    JsonApi.Response.unsubscribe = ""; JsonApi.Response.results = []; JsonApi.Response.feed = []; JsonApi.Response.error = "error in subscribing, user does not exist"}
                    let res = Json.JsonConvert.SerializeObject(data)
                    let byteResponse =
                          res
                            |> System.Text.Encoding.ASCII.GetBytes
                            |> ByteSegment
                    do! ws.send Text byteResponse true 
                }
                Async.StartAsTask s |> ignore 
            return! loop(numTweets, total_users, online_users, offline_users, displayCounter, updateTotalRequests)

        | TwitterCommon.EngineMessage.Unsubscribe(ws, username, userToUnsubscribe) -> 

            let foundRows = userTable.Select("UserName = '" + userToUnsubscribe + "'")
            if foundRows.Length <> 0 then 
                let subscriber_str = foundRows.[0].["Subscribers"].ToString()
                let subscriber_chunk = (((subscriber_str.Split '[').[1].Split ']').[0]).Split ';'
                let mutable subscriber_list = []
                for subscriber in subscriber_chunk do
                    subscriber_list <- List.append subscriber_list [subscriber.Trim()]                                           
                foundRows.[0].["Subscribers"] <- removeElement username subscriber_list
                let s=socket {
                    let data = {JsonApi.Response.username = username |> string; JsonApi.Response.func = "Unsubscribe"; JsonApi.Response.success = "True"; 
                                    JsonApi.Response.tweet = ""; JsonApi.Response.hashtag= ""; JsonApi.Response.mention = ""; JsonApi.Response.subscribe = ""; 
                                    JsonApi.Response.unsubscribe = userToUnsubscribe; JsonApi.Response.results = []; JsonApi.Response.feed = [];JsonApi.Response.error = "" }
                    let res = Json.JsonConvert.SerializeObject(data)
                    let byteResponse =
                          res
                            |> System.Text.Encoding.ASCII.GetBytes
                            |> ByteSegment
                    do! ws.send Text byteResponse true 
                }
                Async.StartAsTask s |> ignore   
            else 
                let s=socket {
                    let data = {JsonApi.Response.username = username |> string; JsonApi.Response.func = "Unsubscribe"; JsonApi.Response.success = "False"; 
                                    JsonApi.Response.tweet = ""; JsonApi.Response.hashtag= ""; JsonApi.Response.mention = ""; JsonApi.Response.subscribe = ""; 
                                    JsonApi.Response.unsubscribe = userToUnsubscribe; JsonApi.Response.results = []; JsonApi.Response.feed = [];JsonApi.Response.error = "error in unsubscribing. User does not exist"}
                    let res = Json.JsonConvert.SerializeObject(data)
                    let byteResponse =
                          res
                            |> System.Text.Encoding.ASCII.GetBytes
                            |> ByteSegment
                    do! ws.send Text byteResponse true 
                }
                Async.StartAsTask s |> ignore 
            return! loop(numTweets, total_users, online_users, offline_users,displayCounter, updateTotalRequests)
        | _ -> failwith "unknown message"
        }
    loop (0, 0, 0, 0, 0, 0)


