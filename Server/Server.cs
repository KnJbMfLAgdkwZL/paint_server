using System;
using System.Collections.Generic;
using System.Text;
using System.Net.Sockets;
using System.Net;
using System.Text.RegularExpressions;
using System.Security.Cryptography;
using Newtonsoft.Json;
using System.Threading;
using System.Linq;
namespace Server
{
    public class StateObject
    {
        public Socket workSocket = null;
        public const int BufferSize = 1024;
        public Byte[] buffer = new Byte[BufferSize];
        public Byte[] total = new Byte[BufferSize];
        public int totallen = 0;
        public StringBuilder sb = new StringBuilder();
    }
    class AsyncWebSocketsServer
    {
        private Dictionary<String, List<User>> rooms = new Dictionary<String, List<User>>();
        private Dictionary<String, List<String>> roomshistory = new Dictionary<String, List<String>>();
        public void Start()
        {
            for (int i = 6780; i < 6789; i++)
            {
                Socket server = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                server.Bind(new IPEndPoint(IPAddress.Any, i));
                server.Listen(100);
                server.BeginAccept(new AsyncCallback(this.EndAccept), server);
            }
            Thread check = new Thread(this.CheckUsers);
            check.IsBackground = true;
            check.Start();
        }
        void Close(Socket client)
        {
            client.Shutdown(SocketShutdown.Both);
            client.BeginDisconnect(true, new AsyncCallback(DisconnectCallback), client);
        }
        private void DisconnectCallback(IAsyncResult ar)
        {
            Socket client = (Socket)ar.AsyncState;
            client.EndDisconnect(ar);
            client.Close();
        }
        private void EndAccept(IAsyncResult ar)
        {
            Socket server = ar.AsyncState as Socket;

            Socket client = server.EndAccept(ar);

            StateObject state = new StateObject();
            state.workSocket = client;

            client.BeginReceive(state.buffer, 0, StateObject.BufferSize, 0, new AsyncCallback(this.OnRecievedData), state);

            server.BeginAccept(new AsyncCallback(this.EndAccept), server);
        }
        private void OnRecievedData(IAsyncResult ar)
        {
            try
            {
                StateObject state = ar.AsyncState as StateObject;
                Socket client = state.workSocket;
                int length = client.EndReceive(ar);
                if (length > 0)
                {
                    Byte[] data = new Byte[length];
                    Array.Copy(state.buffer, data, length);
                    int fin = data[0] & 0x01;
                    Array.Copy(data, 0, state.total, state.totallen, length);
                    state.totallen += length;
                    if (fin == 1)
                    {
                        String str = this.ByteToString(state.total, state.totallen);
                        if (new Regex("^GET").IsMatch(str))
                        {
                            str = this.Handshake(str);
                            this.Send(client, str);
                        }
                        else
                        {
                            str = this.GetDecodedData(state.total, state.totallen);
                            if (str != "-1" && str.Length > 10)
                            {
                                this.MessageHandler(client, str);
                            }
                        }
                        state.totallen = 0;
                    }
                }
                client.BeginReceive(state.buffer, 0, StateObject.BufferSize, 0, new AsyncCallback(this.OnRecievedData), state);
            }
            catch (Exception err)
            {
                return;
            }
        }
        private void Send(Socket handler, String data)
        {
            byte[] datab = this.StringToByte(data);
            this.Send(handler, datab);
        }
        private void Send(Socket handler, byte[] data)
        {
            try
            {
                handler.BeginSend(data, 0, data.Length, 0, new AsyncCallback(SendCallback), handler);
            }
            catch (Exception err)
            {
            }
        }
        private void SendW(Socket handler, String data)
        {
            List<byte> buff = new List<byte>();

            buff.Add(0x81);
            buff.Add(Convert.ToByte(data.Length));
            buff.AddRange(this.StringToByte(data));

            Byte[] datab = buff.ToArray();
            this.Send(handler, datab);
        }
        private void SendCallback(IAsyncResult ar)
        {
            Socket handler = ar.AsyncState as Socket;
            int bytesSent = handler.EndSend(ar);
        }
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
        private String Handshake(String data)
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
            return str;
        }
        public String GetDecodedData(byte[] buffer, int length)
        {
            try
            {
                byte b = buffer[1];
                int dataLength = 0;
                int totalLength = 0;
                int keyIndex = 0;
                if (b - 128 <= 125)
                {
                    dataLength = b - 128;
                    keyIndex = 2;
                    totalLength = dataLength + 6;
                }
                if (b - 128 == 126)
                {
                    dataLength = BitConverter.ToInt16(new byte[] { buffer[3], buffer[2] }, 0);
                    keyIndex = 4;
                    totalLength = dataLength + 8;
                }
                if (b - 128 == 127)
                {
                    dataLength = (int)BitConverter.ToInt64(new byte[] { buffer[9], buffer[8], buffer[7], buffer[6], buffer[5], buffer[4], buffer[3], buffer[2] }, 0);
                    keyIndex = 10;
                    totalLength = dataLength + 14;
                }
                if (totalLength > length)
                {
                    return "-1";
                    //throw new Exception("The buffer length is small than the data length " + totalLength + " > " + length);///////
                }
                byte[] key = new byte[] { buffer[keyIndex], buffer[keyIndex + 1], buffer[keyIndex + 2], buffer[keyIndex + 3] };
                int dataIndex = keyIndex + 4;
                int count = 0;
                for (int i = dataIndex; i < totalLength; i++)
                {
                    buffer[i] = (byte)(buffer[i] ^ key[count % 4]);
                    count++;
                }
                return Encoding.ASCII.GetString(buffer, dataIndex, dataLength);
            }
            catch (Exception err)
            {
                return "-1";
            }
        }
        private void MessageHandler(Socket client, String data)
        {
            List<Object> values = JsonConvert.DeserializeObject<List<Object>>(data);
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
                                this.rooms.Add(room, new List<User>());
                                this.roomshistory.Add(room, new List<String>());
                            }

                            int unixTime = this.UnixTime();
                            String name = "User_" + unixTime;

                            User usr = new User(client, name, values[1] as String, unixTime, room);
                            this.rooms[room].Add(usr);

                            Console.WriteLine(usr.ToString() + " " + room + " " + this.rooms[room].Count);

                            this.UpdateUsers(room);

                            if (this.rooms[room].Count > 1)
                            {
                                this.SetField(room, client);
                            }
                        }
                        break;
                    case "live":
                        {
                            if (this.rooms.ContainsKey(room) == true)
                            {
                                User usr = this.FindUser(room, client);
                                if (usr != null)
                                {
                                    int unixTime = this.UnixTime();
                                    usr.lastmes = unixTime;
                                }
                            }
                        }
                        break;
                    case "nick":
                        {
                            String name = values[1] as String;
                            if (name.Length > 1)
                            {
                                User usr = this.FindUser(room, client);
                                if (usr != null)
                                {
                                    usr.name = name;
                                    this.UpdateUsers(room);
                                }
                            }
                        }
                        break;
                    case "mclk":
                        {
                            this.roomshistory[room].Add(data);
                            this.SendForAll(data, room);
                        }
                        break;
                    case "dlin":
                        {
                            this.roomshistory[room].Add(data);
                            this.SendForAll(data, room);
                        }
                        break;
                    case "clca":
                        {
                            this.roomshistory[room].Clear();
                            this.SendForAll(data, room);
                        }
                        break;
                }
            }
        }
        private User FindUser(String room, Socket client)
        {
            List<User> users = this.rooms[room];
            for (int i = 0; i < users.Count; i++)
            {
                User item = users[i];
                if (item.EqualsClient(client) == true)
                {
                    return item;
                }
            }
            return null;
        }
        private void SetField(String room, Socket client)
        {
            List<String> history = this.roomshistory[room];
            for (int i = 0; i < history.Count; i++)
            {
                String str = history[i];
                this.SendW(client, str);
            }
        }
        private void UpdateUsers(String room)
        {
            List<String> names = new List<String>();
            List<User> users = this.rooms[room];
            for (int i = 0; i < users.Count; i++)
            {
                User item = users[i];
                String str = item.name;
                names.Add(str);
            }
            Object[] command = { "upus", names };
            this.SendForAll(command, room);
        }
        private int UnixTime()
        {
            int unixTime = (int)(DateTime.UtcNow - new DateTime(1970, 1, 1)).TotalSeconds;
            return unixTime;
        }
        private void SendForAll(Object[] command, String room, Socket except = null)
        {
            String data = JsonConvert.SerializeObject(command);
            this.SendForAll(data, room, except);
        }
        private void SendForAll(String data, String room, Socket except = null)
        {
            List<User> users = this.rooms[room];
            for (int i = 0; i < users.Count; i++)
            {
                Socket client = users[i].GetClient();
                if (client.Equals(except) == false)
                {
                    this.SendW(client, data);
                }
            }
        }
        private void CheckUsers()
        {
            while (true)
            {
                Thread.Sleep(1000);
                for (int i = 0; i < this.rooms.Count; i++)
                {
                    int unixTime = this.UnixTime();
                    List<User> users = this.rooms.ElementAt(i).Value;
                    for (int j = 0; j < users.Count; j++)
                    {
                        User usr = users[j];
                        long last = usr.lastmes;
                        Socket client = usr.GetClient();
                        if (client.Connected == false || last + 5 < unixTime)
                        {
                            users.Remove(usr);

                            this.Close(client);

                            String room = usr.roomid;
                            if (users.Count > 0)
                            {
                                this.UpdateUsers(room);
                            }
                            else
                            {
                                users.Clear();
                                this.rooms.Remove(room);
                                this.roomshistory[room].Clear();
                                this.roomshistory.Remove(room);
                            }
                        }
                    }
                }
            }
        }
    }
}