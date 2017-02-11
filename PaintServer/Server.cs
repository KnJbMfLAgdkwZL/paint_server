using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Newtonsoft.Json;
using Alchemy;
using Alchemy.Classes;
namespace PaintServer
{
    class Server
    {
        private ConcurrentDictionary<String, ConcurrentDictionary<int, User>> rooms = new ConcurrentDictionary<String, ConcurrentDictionary<int, User>>();
        private ConcurrentDictionary<String, ConcurrentDictionary<int, String>> history = new ConcurrentDictionary<String, ConcurrentDictionary<int, String>>();
        WebSocketServer aServer;
        public void Start(int port)
        {
            try
            {
                aServer = new WebSocketServer(port, IPAddress.Any)
                {
                    OnReceive = this.OnReceive,
                    OnSend = this.OnSend,
                    OnConnected = this.OnConnect,
                    OnDisconnect = this.OnDisconnect,
                    TimeOut = new TimeSpan(0, 5, 0)
                };
                aServer.Start();
                Console.WriteLine("Start()");
            }
            catch (Exception e)
            {
            }
        }
        public void Stop()
        {
            try
            {
                aServer.Stop();
                Console.WriteLine("Stop()");
            }
            catch (Exception e)
            {
            }
        }
        private void OnReceive(UserContext context)
        {
            try
            {
                String json = context.DataFrame.ToString();
                this.MessageHandler(context, json);
            }
            catch (Exception e)
            {
            }
        }
        private void OnSend(UserContext context)
        {
            //Console.WriteLine("Data Send To : " + context.ClientAddress);
        }
        private void OnConnect(UserContext context)
        {
            //Console.WriteLine("Client Connection From : " + context.ClientAddress);
        }
        private void OnDisconnect(UserContext context)
        {
            //Console.WriteLine("Client Disconnected : " + context.ClientAddress);
            try
            {
                User user = this.RemoveUser(context);
                if (user != null)
                {
                    this.UpdateUsers(user.room);
                }
            }
            catch (Exception e)
            {
            }
        }
        ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        private void MessageHandler(UserContext client, String data)
        {
            List<Object> values = null;
            try
            {
                values = JsonConvert.DeserializeObject<List<Object>>(data);
            }
            catch (Exception err)
            {
                return;
            }
            try
            {
                if (values.Count > 0)
                {
                    String room = values.Last() as String;
                    String command = values.First() as String;
                    switch (command)
                    {
                        case "conn":
                            {
                                if (this.rooms.ContainsKey(room) == false)
                                {
                                    this.rooms.TryAdd(room, new ConcurrentDictionary<int, User>());
                                    this.history.TryAdd(room, new ConcurrentDictionary<int, String>());
                                }
                                int unixTime = this.UnixTime();
                                String name = "User_" + unixTime;
                                String info = values[1] as String;
                                User user = new User(client, name, info, room, unixTime);
                                this.rooms[room].TryAdd(unixTime, user);
                                this.UpdateUsers(room);
                                if (this.rooms[room].Count > 1)
                                {
                                    this.SetField(room, client);
                                }
                            }
                            break;
                        case "nick":
                            {
                                String name = values[1] as String;
                                if (name.Length > 1)
                                {
                                    User user = this.FindUser(room, client);
                                    if (user != null)
                                    {
                                        user.name = name;
                                        this.UpdateUsers(room);
                                    }
                                }
                            }
                            break;
                        case "mclk":
                            {
                                this.history[room].TryAdd(this.history[room].Count, data);
                                this.SendForAll(data, room);
                            }
                            break;
                        case "dlin":
                            {
                                this.history[room].TryAdd(this.history[room].Count, data);
                                this.SendForAll(data, room);
                            }
                            break;
                        case "clca":
                            {
                                this.history[room].Clear();
                                this.SendForAll(data, room);
                            }
                            break;
                    }
                }
            }
            catch (Exception e)
            {
            }
        }
        private void SetField(String room, UserContext client)
        {
            try
            {
                if (this.history.ContainsKey(room) == true)
                {
                    ConcurrentDictionary<int, String> history = this.history[room];
                    for (int i = 0; i < history.Count; i++)
                    {
                        String str = history.ElementAt(i).Value;
                        client.Send(str);
                    }
                }
            }
            catch (Exception e)
            {
            }
        }
        private User FindUser(String room, UserContext client)
        {
            try
            {
                if (this.rooms.ContainsKey(room) == true)
                {
                    ConcurrentDictionary<int, User> users = this.rooms[room];
                    for (int i = 0; i < users.Count; i++)
                    {
                        User item = users.ElementAt(i).Value;
                        if (item.client.Equals(client) == true)
                        {
                            return item;
                        }
                    }
                }
            }
            catch (Exception e)
            {
            }
            return null;
        }
        private User RemoveUser(UserContext client)
        {
            try
            {
                for (int i = 0; i < this.rooms.Keys.Count; i++)
                {
                    ConcurrentDictionary<int, User> users = this.rooms.ElementAt(i).Value;
                    for (int y = 0; y < users.Keys.Count; y++)
                    {
                        int key = users.ElementAt(y).Key;
                        User user = users.ElementAt(y).Value;
                        if (user.time == key)
                        {
                            if (user.client.Equals(client) == true)
                            {
                                users.TryRemove(key, out user);

                                String room = user.room;
                                if (users.Count <= 0)
                                {
                                    users.Clear();
                                    this.rooms.TryRemove(room, out users);
                                    ConcurrentDictionary<int, String> hist = this.history[room];
                                    hist.Clear();
                                    this.history.TryRemove(room, out hist);
                                }
                                return user;
                            }
                        }
                    }
                }
            }
            catch (Exception e)
            {
            }
            return null;
        }
        private void UpdateUsers(String room)
        {
            try
            {
                if (this.rooms.ContainsKey(room) == true)
                {
                    List<String> names = new List<String>();
                    ConcurrentDictionary<int, User> users = this.rooms[room];
                    for (int i = 0; i < users.Count; i++)
                    {
                        User item = users.ElementAt(i).Value;
                        String str = item.name;
                        names.Add(str);
                    }
                    Object[] command = { "upus", names };
                    this.SendForAll(command, room);
                }
            }
            catch (Exception e)
            {
            }
        }
        private void SendForAll(Object[] command, String room)
        {
            try
            {
                String data = JsonConvert.SerializeObject(command);
                this.SendForAll(data, room);
            }
            catch (Exception e)
            {
            }
        }
        private void SendForAll(String data, String room)
        {
            try
            {
                if (this.rooms.ContainsKey(room) == true)
                {
                    ConcurrentDictionary<int, User> users = this.rooms[room];
                    for (int i = 0; i < users.Count; i++)
                    {
                        UserContext client = users.ElementAt(i).Value.client;
                        client.Send(data);
                    }
                }
            }
            catch (Exception e)
            {
            }
        }
        private int UnixTime()
        {
            try
            {
                int unixTime = (int)(DateTime.UtcNow - new DateTime(1970, 1, 1)).TotalSeconds;
                return unixTime;
            }
            catch (Exception e)
            {
            }
            return 0;
        }
        ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    }
}