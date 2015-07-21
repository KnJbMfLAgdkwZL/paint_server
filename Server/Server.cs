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
    /// <summary>
    ///     Вспомогательный класс для служебной информации.
    /// </summary>
    class User
    {
        private Socket client = null;
        private String name = "";
        private String userinfo = "";
        private long lastmes = 0;
        private String roomid = "";
        public void SetName(String name)
        {
            this.name = name;
        }
        public void SetLastmes(long lastmes)
        {
            this.lastmes = lastmes;
        }
        public Socket GetClient()
        {
            return this.client;
        }
        public String GetName()
        {
            return this.name;
        }
        public String GetRoomid()
        {
            return this.roomid;
        }
        public long GetLastmes()
        {
            return this.lastmes;
        }
        public bool EqualsClient(Socket client)
        {
            return this.client.Equals(client);
        }
        public User(Socket client, String name, String userinfo, long lastmes, String roomid)
        {
            this.client = client;
            this.name = name;
            this.userinfo = userinfo;
            this.lastmes = lastmes;
            this.roomid = roomid;
        }
        /// <summary>
        ///     Вывести информацию по пользователю в консоль.
        /// </summary>
        public void ShowAll()
        {
            String str = "";
            str += this.client.RemoteEndPoint;
            str += " " + name;
            str += " " + this.lastmes;
            str += " " + this.userinfo;
            Console.WriteLine(str);
        }
    }
    class AsyncWebSocketsServer
    {
        private Byte[] buffer = new Byte[10240];
        private Socket server = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        private Dictionary<string, List<User>> rooms = new Dictionary<string, List<User>>();
        public void Start(int port)
        {
            this.server.Bind(new IPEndPoint(IPAddress.Any, port));
            this.server.Listen(10);
            this.server.BeginAccept(new AsyncCallback(this.EndAccept), this.server);
            //
            Thread th = new Thread(this.CheckStilalive);
            th.IsBackground = true;
            th.Start();
        }
        public void Stop()
        {
            for (int i = 0; i < this.rooms.Count; i++)
            {
                //Socket tmp = this.rooms[i];
                //tmp.BeginDisconnect(true, new AsyncCallback(DisconnectCallback), tmp);
                //this.rooms.Remove(tmp);
            }
            this.server.Close();
        }
        private void DisconnectCallback(IAsyncResult ar)
        {
            Socket tmp = (Socket)ar.AsyncState;
            tmp.EndDisconnect(ar);
            tmp.Close();
        }
        private void EndAccept(IAsyncResult ar)
        {
            this.server = ar.AsyncState as Socket;
            Socket сlient = this.server.EndAccept(ar);

            //this.users.Add(сlient);

            this.SetupRecieveCallback(сlient);
            this.server.BeginAccept(new AsyncCallback(this.EndAccept), this.server);
        }
        private void SetupRecieveCallback(Socket сlient)
        {
            try
            {
                AsyncCallback recieveData = new AsyncCallback(this.OnRecievedData);
                сlient.BeginReceive(this.buffer, 0, this.buffer.Length, SocketFlags.None, recieveData, сlient);
            }
            catch (Exception err)
            {
                Console.WriteLine(err.Message);
                Console.WriteLine(err.Source);
                Console.WriteLine(err.StackTrace.Substring(err.StackTrace.LastIndexOf("at")));
                сlient.Shutdown(SocketShutdown.Both);
                сlient.Close();
                //this.users.Remove(сlient);
                return;
            }
        }
        private void OnRecievedData(IAsyncResult ar)
        {
            Socket client = ar.AsyncState as Socket;
            int length = client.EndReceive(ar);
            if (length > 0)
            {
                byte[] buff = new byte[length];
                Array.Copy(this.buffer, buff, length);
                //
                String data = Encoding.UTF8.GetString(buff, 0, length);
                if (new Regex("^GET").IsMatch(data))
                {
                    this.Handshake(client, data);
                }
                else
                {
                    buff = this.DecodeMessag(buff);
                    data = this.BufferToStr(buff);
                    this.ServerLogic(data, client);
                    //this.SendStrForWbebSocet(client, "This String from C# this Hello World");
                }
                //
                SetupRecieveCallback(client);
            }
        }
        private void ServerLogic(String data, Socket client)
        {
            try
            {
                //Console.WriteLine(data);
                Dictionary<string, string> values = JsonConvert.DeserializeObject<Dictionary<string, string>>(data);
                switch (values["command"])
                {
                    case "room":
                        {
                            String rooomid = values["id"];
                            if (!this.rooms.ContainsKey(rooomid))
                            {
                                this.rooms.Add(rooomid, new List<User>());
                                Console.WriteLine("Комната создана");
                            }
                            int unixTime = (int)(DateTime.UtcNow - new DateTime(1970, 1, 1)).TotalSeconds;
                            String name = "User_" + unixTime;
                            User usr = new User(client, name, values["userinfo"], unixTime, rooomid);
                            this.rooms[rooomid].Add(usr);
                            usr.ShowAll();
                            Console.WriteLine("Room " + rooomid + " Count = " + this.rooms[rooomid].Count);
                        }
                        break;
                    case "stilalive":
                        {
                            String rooomid = values["id"];
                            if (!this.rooms.ContainsKey(rooomid))
                            {
                                return;
                            }
                            List<User> room = this.rooms[rooomid];
                            for (int i = 0; i < room.Count; i++)
                            {
                                User usr = room[i];
                                if (usr.EqualsClient(client) == true)
                                {
                                    int unixTime = (int)(DateTime.UtcNow - new DateTime(1970, 1, 1)).TotalSeconds;
                                    usr.SetLastmes(unixTime);
                                    Console.WriteLine(unixTime + " = " + usr.GetName());
                                    break;
                                }
                            }
                        }
                        break;
                }
            }
            catch (Exception)
            {

            }
        }
        private void CheckStilalive()
        {
            while (true)
            {
                Thread.Sleep(1000);
                try
                {
                    foreach (KeyValuePair<string, List<User>> users in this.rooms)
                    {
                        for (int i = 0; i < users.Value.Count; i++)
                        {
                            User usr = users.Value[i];
                            long last = usr.GetLastmes();
                            int unixTime = (int)(DateTime.UtcNow - new DateTime(1970, 1, 1)).TotalSeconds;
                            if (last + 10 < unixTime)
                            {
                                String roomid = usr.GetRoomid();
                                this.rooms[roomid].Remove(usr);
                                Console.WriteLine(usr.GetName() + " Вышол");
                                this.Close(usr.GetClient());
                            }

                        }
                    }
                }
                catch (Exception err)
                {
                    Console.WriteLine(err.Message);
                }
            }
        }
        private void Close(Socket s)
        {
            s.BeginDisconnect(true, new AsyncCallback(DisconnectCallback), s);
        }

        private void Handshake(Socket client, String data)
        {
            byte[] MASK = new byte[4];
            String key = new Regex("Sec-WebSocket-Key: (.*)").Match(data).Groups[1].Value.Trim();
            MASK = SHA1.Create().ComputeHash(Encoding.UTF8.GetBytes(key + "258EAFA5-E914-47DA-95CA-C5AB0DC85B11"));
            String str = "";
            str += "HTTP/1.1 101 Switching Protocols" + Environment.NewLine;
            str += "Connection: Upgrade" + Environment.NewLine;
            str += "Upgrade: websocket" + Environment.NewLine;
            str += "Sec-WebSocket-Accept: ";
            str += Convert.ToBase64String(MASK);
            str += Environment.NewLine;
            str += Environment.NewLine;
            this.SendTo(client, str);
        }
        private Byte[] DecodeMessag(Byte[] buffer)
        {
            int end = 2;
            long bodylen = buffer[1] & 0x7F;
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
            bodylen = buffer.Length - endmask;
            Byte[] decoded = new Byte[bodylen];
            Byte[] encoded = new Byte[bodylen];
            for (int i = 0; i < buffer.Length; i++)
            {
                if (i >= end && i < endmask)
                {
                    mask[i - end] = buffer[i];
                }
                if (i >= endmask)
                {
                    encoded[i - endmask] = buffer[i];
                }
            }
            for (int i = 0; i < encoded.Length; i++)
            {
                decoded[i] = (Byte)(encoded[i] ^ mask[i % 4]);
            }
            return decoded;
        }
        private String BufferToStr(Byte[] buffer)
        {
            String str = "";
            for (int i = 0; i < buffer.Length; i++)
            {
                str += (char)buffer[i];
            }
            return str;
        }
        private void SendTo(Socket client, String str)
        {
            byte[] sendbuf = Encoding.UTF8.GetBytes(str);
            client.Send(sendbuf);
        }
        private void SendToAll(Byte[] buffer, Socket socket = null)
        {
            /*for (int i = 0; i < this.users.Count; i++)
            {
                Socket clientSend = this.users[i];
                if (socket != clientSend)
                {
                    clientSend.Send(buffer);
                }
            }*/
        }
        /// <summary>
        ///     Отправляет на веб сокет строку.
        /// </summary>
        private void SendStrForWbebSocet(Socket client, String str)
        {
            List<byte> buff = new List<byte>();
            buff.Add(0x81);
            buff.Add(Convert.ToByte(str.Length));
            buff.AddRange(Encoding.UTF8.GetBytes(str));
            client.Send(buff.ToArray());
        }
    }
}