using System.Linq;
using System;
using System.Collections.Generic;
using System.Text;
using System.Net.Sockets;
using System.Net;
using System.Text.RegularExpressions;
using System.Security.Cryptography;
using Newtonsoft.Json;
using System.Threading;
namespace Server
{
    class Server_2
    {
        private Socket server;
        Thread newconnections;
        private Dictionary<string, List<User>> rooms = new Dictionary<string, List<User>>();
        private String ByteToString(Byte[] data, int length = 0)
        {
            if (length <= 0)
            {
                length = data.Length;
            }
            return Encoding.UTF8.GetString(data, 0, length);
        }
        private Byte[] StringToByte(String data)
        {
            return Encoding.UTF8.GetBytes(data);
        }
        private void SendTo(String data, Socket client)
        {
            Byte[] bytes = this.StringToByte(data);
            this.SendTo(bytes, client);
        }
        private void SendTo(Byte[] data, Socket client)
        {
            try
            {
                client.Send(data);//
            }
            catch (Exception err)
            {
                Console.WriteLine(err.Message);
                Console.WriteLine(err.Source);
                Console.WriteLine(err.StackTrace.Substring(err.StackTrace.LastIndexOf("at")));
            }
        }
        private void SendToW(String data, Socket client)
        {
            List<byte> buff = new List<byte>();
            buff.Add(0x81);
            buff.Add(Convert.ToByte(data.Length));
            buff.AddRange(this.StringToByte(data));

            Byte[] datab = buff.ToArray();
            this.SendTo(datab, client);
        }
        private void SendForAll(Object[] command, String room, Socket except = null)
        {
            String data = JsonConvert.SerializeObject(command);
            foreach (User item in this.rooms[room])
            {
                Socket client = item.GetClient();
                if (client.Equals(except) == false)
                {
                    this.SendToW(data, client);
                }
            }
        }
        private void SendForAll(String data, String room, Socket except = null)
        {
            foreach (User item in this.rooms[room])
            {
                Socket client = item.GetClient();
                if (client.Equals(except) == false)
                {
                    this.SendToW(data, client);
                }
            }
        }
        private void Handshake(String data, Socket client)
        {
            Byte[] MASK = new Byte[4];
            String key = new Regex("Sec-WebSocket-Key: (.*)").Match(data).Groups[1].Value.Trim();
            MASK = SHA1.Create().ComputeHash(this.StringToByte(key + "258EAFA5-E914-47DA-95CA-C5AB0DC85B11"));
            String str = "";
            str += "HTTP/1.1 101 Switching Protocols" + Environment.NewLine;
            str += "Connection: Upgrade" + Environment.NewLine;
            str += "Upgrade: websocket" + Environment.NewLine;
            str += "Sec-WebSocket-Accept: " + Convert.ToBase64String(MASK) + Environment.NewLine;
            str += Environment.NewLine;
            this.SendTo(str, client);
        }
        private int UnixTime()
        {
            int unixTime = (int)(DateTime.UtcNow - new DateTime(1970, 1, 1)).TotalSeconds;
            return unixTime;
        }
        private Byte[] Decode(Byte[] data)
        {
            int end = 2;
            long bodylen = data[1] & 0x7F;
            if (bodylen == 126 || bodylen == 127)
            {
                if (bodylen == 126)
                {
                    end += 2;
                }
                else if (bodylen == 127)
                {
                    end += 8;
                }
            }
            Byte[] mask = new Byte[4];
            int endmask = end + mask.Length;
            bodylen = data.Length - endmask;
            Byte[] decoded = new Byte[bodylen];
            Byte[] encoded = new Byte[bodylen];
            for (int i = 0; i < data.Length; i++)
            {
                if (i >= end && i < endmask)
                {
                    mask[i - end] = data[i];
                }
                if (i >= endmask)
                {
                    encoded[i - endmask] = data[i];
                }
            }
            for (int i = 0; i < encoded.Length; i++)
            {
                decoded[i] = (Byte)(encoded[i] ^ mask[i % 4]);
            }
            return decoded;
        }
        private User FindUser(String room, Socket client)
        {
            foreach (User item in this.rooms[room])
            {
                if (item.EqualsClient(client) == true)
                {
                    return item;
                }
            }
            return null;
        }
        private void UpdateUsers(String room)
        {
            List<String> names = new List<String>();
            List<User> users = this.rooms[room];
            foreach (User user in this.rooms[room])
            {
                String str = user.GetName();
                names.Add(str);
            }
            Object[] command = { "UpdateUsers", names };
            this.SendForAll(command, room);
        }
        public void Start(int port)
        {
            this.server = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            this.server.Bind(new IPEndPoint(IPAddress.Any, port));
            this.server.Listen(100);
            this.newconnections = new Thread(this.ReceivingConnect);
            this.newconnections.IsBackground = true;
            this.newconnections.Start();
            //
            Thread check = new Thread(this.CheckUsers);
            check.IsBackground = true;
            check.Start();
        }
        private void Close(Socket client)
        {
            try
            {
                client.Close();
            }
            catch (Exception err)
            {
                Console.WriteLine(err.Message);
                Console.WriteLine(err.Source);
                Console.WriteLine(err.StackTrace.Substring(err.StackTrace.LastIndexOf("at")));
                return;
            }
        }
        private void CheckUsers()
        {
            while (true)
            {
                Thread.Sleep(100);
                for (int i = 0; i < this.rooms.Count; i++)
                {
                    List<User> users = this.rooms.ElementAt(i).Value;
                    for (int j = 0; j < users.Count; j++)
                    {
                        User usr = users[j];
                        long last = usr.GetLastmes();
                        int unixTime = this.UnixTime();
                        if (last + 2 < unixTime)
                        {
                            String room = usr.GetRoomid();
                            this.rooms[room].Remove(usr);
                            this.Close(usr.GetClient());
                            if (this.rooms[room].Count > 0)
                            {
                                this.UpdateUsers(room);
                            }
                            else
                            {
                                this.rooms.Remove(room);
                                this.CheckUsers();
                                return;
                            }
                        }
                    }
                }
            }
        }
        private void ReceivingConnect()
        {
            while (true)
            {
                Socket client = this.server.Accept();
                Thread clientthread = new Thread(new ParameterizedThreadStart(this.OnRecievedData));
                clientthread.IsBackground = true;
                Object[] arr = { client, clientthread };
                clientthread.Start(arr);
            }
        }
        private void OnRecievedData(Object obj)
        {
            Object[] param = obj as Object[];
            Socket client = param[0] as Socket;
            Thread curthr = param[1] as Thread;
            while (client.Connected == true)
            {
                try
                {
                    Byte[] buffer = new Byte[client.ReceiveBufferSize];
                    int length = client.Receive(buffer);//Ошибка
                    if (length > 0)
                    {
                        Byte[] data = new Byte[length];
                        Array.Copy(buffer, data, length);
                        String str = this.ByteToString(data, length);
                        if (new Regex("^GET").IsMatch(str))
                        {
                            this.Handshake(str, client);
                        }
                        else
                        {
                            data = this.Decode(data);
                            str = this.ByteToString(data);
                            if (str.Length > 10)
                            {
                                this.MessageHandler(str, client);
                            }
                        }
                        Console.WriteLine(str);
                    }
                }
                catch (Exception err)
                {
                    Console.WriteLine(err.Message);
                    Console.WriteLine(err.Source);
                    Console.WriteLine(err.StackTrace.Substring(err.StackTrace.LastIndexOf("at")));
                    this.Close(client);
                    return;
                }
            }
        }
        private void MessageHandler(String data, Socket client)
        {
            try
            {
                Dictionary<string, string> values = JsonConvert.DeserializeObject<Dictionary<string, string>>(data);
                if (values.Count > 0)
                {
                    switch (values["command"])
                    {
                        case "room":
                            {
                                String room = values["id"];
                                if (this.rooms.ContainsKey(room) == false)
                                {
                                    this.rooms.Add(room, new List<User>());
                                }
                                int unixTime = this.UnixTime();
                                String name = "User_" + unixTime;
                                User usr = new User(client, name, values["userinfo"], unixTime, room);
                                this.rooms[room].Add(usr);
                                usr.ShowAll();
                                Console.WriteLine("Room " + room + " Count = " + this.rooms[room].Count);
                                this.UpdateUsers(room);
                            }
                            break;
                        case "stilalive":
                            {
                                String room = values["id"];
                                if (this.rooms.ContainsKey(room) == true)
                                {
                                    User usr = this.FindUser(room, client);
                                    if (usr != null)
                                    {
                                        int unixTime = this.UnixTime();
                                        usr.SetLastmes(unixTime);
                                    }
                                }
                            }
                            break;
                        case "changenick":
                            {
                                String name = values["name"];
                                if (name.Length > 1)
                                {
                                    String room = values["id"];
                                    User usr = this.FindUser(room, client);
                                    if (usr != null)
                                    {
                                        usr.SetName(name);
                                        this.UpdateUsers(room);
                                    }
                                }
                            }
                            break;
                        case "mousepaintclick":
                            {
                                String room = values["id"];
                                data = JsonConvert.SerializeObject(values.Values);
                                this.SendForAll(data, room);
                            }
                            break;
                    }
                }
            }
            catch (Exception err)
            {
                Console.WriteLine(err.Message);
                Console.WriteLine(err.Source);
                Console.WriteLine(err.StackTrace.Substring(err.StackTrace.LastIndexOf("at")));

                this.Close(client);
                return;
            }
        }
    }
}