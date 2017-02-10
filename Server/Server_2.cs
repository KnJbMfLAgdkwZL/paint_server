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
        private Dictionary<String, List<User>> rooms = new Dictionary<String, List<User>>();
        private Dictionary<String, List<String>> roomshistory = new Dictionary<String, List<String>>();
        private String ByteToString(Byte[] data, int length = 0)
        {
            try
            {
                if (length <= 0)
                {
                    length = data.Length;
                }
                return Encoding.UTF8.GetString(data, 0, length);
            }
            catch (Exception err)
            {
                Console.WriteLine(err);
                return "";
            }
        }
        private Byte[] StringToByte(String data)
        {
            return Encoding.UTF8.GetBytes(data);
        }
        private void SendTo(String data, Socket client)
        {
            try
            {
                Console.WriteLine(data);
                Byte[] bytes = this.StringToByte(data);
                this.SendTo(bytes, client);
            }
            catch (Exception err)
            {
                Console.WriteLine(err);
                return;
            }
        }
        private void SendTo(Byte[] data, Socket client)
        {
            try
            {
                client.Send(data);
            }
            catch (Exception err)
            {
                Console.WriteLine(err);
                return;
            }
        }
        private void SendToW(String data, Socket client)
        {
            try
            {
                Console.WriteLine(data);
                List<byte> buff = new List<byte>();
                buff.Add(0x81);
                buff.Add(Convert.ToByte(data.Length));
                buff.AddRange(this.StringToByte(data));

                Byte[] datab = buff.ToArray();
                this.SendTo(datab, client);
            }
            catch (Exception err)
            {
                Console.WriteLine(err);
            }
        }
        private void SendForAll(Object[] command, String room, Socket except = null)
        {
            try
            {
                foreach (Object i in command)
                {
                    Console.WriteLine(i);
                    Console.WriteLine(i.ToString());
                }
                String data = JsonConvert.SerializeObject(command);
                Console.WriteLine(data);
                for (int i = 0; i < this.rooms[room].Count; i++)
                {
                    try
                    {
                        Socket client = this.rooms[room][i].GetClient();
                        if (client.Equals(except) == false)
                        {
                            Console.WriteLine(data);
                            this.SendToW(data, client);
                        }
                    }
                    catch (Exception)
                    {
                    }
                }
            }
            catch (Exception err)
            {
                Console.WriteLine(err);
            }
        }
        private void SendForAll(String data, String room, Socket except = null)
        {
            Console.WriteLine(data);
            try
            {
                foreach (User item in this.rooms[room])
                {
                    Socket client = item.GetClient();
                    if (client.Equals(except) == false)
                    {
                        Console.WriteLine(data);
                        this.SendToW(data, client);
                    }
                }
            }
            catch (Exception err)
            {
                Console.WriteLine(err);
            }
        }
        private void Handshake(String data, Socket client)
        {
            try
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
            catch (Exception err)
            {
                Console.WriteLine(err);
            }
        }
        private int UnixTime()
        {
            try
            {
                int unixTime = (int)(DateTime.UtcNow - new DateTime(1970, 1, 1)).TotalSeconds;
                return unixTime;
            }
            catch (Exception err)
            {
                Console.WriteLine(err);
                return 0;
            }
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
                Console.WriteLine(err);
                return "-1";
            }
        }
        private Byte[] Decode(Byte[] data)
        {
            try
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
            catch (Exception err)
            {
                Console.WriteLine(err);
                return new Byte[0];
            }
        }
        private User FindUser(String room, Socket client)
        {
            try
            {
                foreach (User item in this.rooms[room])
                {
                    if (item.EqualsClient(client) == true)
                    {
                        return item;
                    }
                }
            }
            catch (Exception err)
            {
                Console.WriteLine(err);
                return null;
            }
            return null;
        }
        private void UpdateUsers(String room)
        {
            try
            {
                List<String> names = new List<String>();
                List<User> users = this.rooms[room];
                foreach (User user in this.rooms[room])
                {
                    String str = user.GetName();
                    names.Add(str);
                }
                Object[] command = { "upus", names };
                this.SendForAll(command, room);
            }
            catch (Exception err)
            {
                Console.WriteLine(err);
                return;
            }
        }
        public void Start()
        {
            try
            {
                for (int port = 6780; port <= 6789; port++)
                {
                    Socket server = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                    server.Bind(new IPEndPoint(IPAddress.Any, port));
                    server.Listen(100);
                    Thread newconnections = new Thread(new ParameterizedThreadStart(this.ReceivingConnect));
                    newconnections.IsBackground = true;
                    newconnections.Start(server);
                }
                /*Thread check = new Thread(this.CheckUsers);
                check.IsBackground = true;
                check.Start();*/
            }
            catch (Exception err)
            {
                Console.WriteLine(err);
            }
        }
        public void Start2()
        {
            try
            {
                for (int port = 6780; port <= 6789; port++)
                {
                    TcpListener server = new TcpListener(IPAddress.Any, port);
                    server.Start();

                    Thread newconnections = new Thread(new ParameterizedThreadStart(this.ReceivingConnect2));
                    newconnections.IsBackground = true;
                    newconnections.Start(server);
                }
                /*Thread check = new Thread(this.CheckUsers);
                check.IsBackground = true;
                check.Start();*/
            }
            catch (Exception err)
            {
                Console.WriteLine(err);
            }
        }
        private void Close(Socket client)
        {
            try
            {
                client.Close();
            }
            catch (Exception err)
            {
                Console.WriteLine(err);
            }
        }
        private void CheckUsers()
        {
            while (true)
            {
                try
                {
                    Thread.Sleep(1000);
                    for (int i = 0; i < this.rooms.Count; i++)
                    {
                        int unixTime = this.UnixTime();
                        List<User> users = this.rooms.ElementAt(i).Value;
                        try
                        {
                            for (int j = 0; j < users.Count; j++)
                            {
                                User usr = users[j];
                                long last = usr.GetLastmes();
                                Socket client = usr.GetClient();
                                if (client.Connected == false || last + 5 < unixTime)
                                {
                                    try
                                    {
                                        users.Remove(usr);
                                        this.Close(client);
                                        String room = usr.GetRoom();
                                        if (users.Count > 0)
                                        {
                                            this.UpdateUsers(room);
                                        }
                                        else
                                        {
                                            users.Clear();
                                            this.rooms.Remove(room);
                                            this.roomshistory.Remove(room);
                                        }
                                    }
                                    catch (Exception err)
                                    {
                                    }
                                }
                            }
                        }
                        catch (Exception err)
                        {
                        }
                    }
                }
                catch (Exception err)
                {
                    Console.WriteLine(err);
                    return;
                }
            }
        }
        private void ReceivingConnect(Object obj)
        {
            Socket server = obj as Socket;
            while (true)
            {
                try
                {
                    Socket client = server.Accept();
                    Thread clientthread = new Thread(new ParameterizedThreadStart(this.OnRecievedData));
                    clientthread.IsBackground = true;
                    Object[] arr = { client, clientthread };
                    clientthread.Start(arr);
                }
                catch (Exception err)
                {
                    Console.WriteLine(err);
                    continue;
                }
            }
        }
        private void ReceivingConnect2(Object obj)
        {
            TcpListener server = obj as TcpListener;
            while (true)
            {
                try
                {
                    Socket client = server.AcceptSocket();
                    Thread clientthread = new Thread(new ParameterizedThreadStart(this.OnRecievedData));
                    clientthread.IsBackground = true;
                    Object[] arr = { client, clientthread };
                    clientthread.Start(arr);
                }
                catch (Exception err)
                {
                    Console.WriteLine(err);
                    continue;
                }
            }
        }
        private void OnRecievedData(Object obj)
        {
            Object[] param = obj as Object[];
            Socket client = param[0] as Socket;
            Thread curthr = param[1] as Thread;
            client.ReceiveBufferSize = 10240;
            Byte[] buffer = new Byte[client.ReceiveBufferSize];
            Byte[] total = new Byte[client.ReceiveBufferSize];
            long totallen = 0;
            while (client.Connected == true)
            {
                try
                {
                    int length = client.Receive(buffer);//Ошибка
                    if (length > 0)
                    {
                        Byte[] data = new Byte[length];
                        Array.Copy(buffer, data, length);
                        int fin = data[0] & 0x01;
                        Array.Copy(data, 0, total, totallen, length);
                        totallen += length;
                        if (fin == 1)
                        {
                            String str = this.ByteToString(total, (int)totallen);
                            if (new Regex("^GET").IsMatch(str))
                            {
                                this.Handshake(str, client);
                            }
                            else
                            {
                                str = this.GetDecodedData(total, (int)totallen);
                                if (str == "-1")
                                {
                                    continue;
                                }
                                if (str.Length > 10)
                                {
                                    this.MessageHandler(str, client);
                                }
                            }
                            Console.WriteLine(str);
                            totallen = 0;
                        }
                    }
                }
                catch (Exception err)
                {
                    Console.WriteLine(err);
                    try
                    {
                        this.Close(client);
                        GC.Collect();
                    }
                    catch (Exception er)
                    {

                        Console.WriteLine(er);
                    }
                    curthr.Interrupt();
                    curthr.Abort();
                    return;
                }
            }
        }
        private void SetField(String room, Socket client)
        {
            try
            {
                List<String> history = this.roomshistory[room];
                for (int i = 0; i < history.Count; i++)
                {
                    String str = history[i];
                    this.SendToW(str, client);
                }
            }
            catch (Exception err)
            {
                Console.WriteLine(err);
            }
        }
        private void MessageHandler(String data, Socket client)
        {
            Console.WriteLine(data);
            try
            {
                List<Object> values = JsonConvert.DeserializeObject<List<Object>>(data);
                if (values.Count > 0)
                {
                    switch (values[0] as String)
                    {
                        case "conn":
                            {
                                String room = values.Last() as String;
                                if (this.rooms.ContainsKey(room) == false)
                                {
                                    this.rooms.Add(room, new List<User>());
                                    this.roomshistory.Add(room, new List<String>());
                                }
                                int unixTime = this.UnixTime();
                                String name = "User_" + unixTime;
                                User usr = new User(client, name, values[1] as String, unixTime, room);
                                this.rooms[room].Add(usr);
                                Console.WriteLine(usr.ToString() + " Room " + room + " Count " + this.rooms[room].Count);
                                this.UpdateUsers(room);
                                if (this.rooms[room].Count > 1)
                                {
                                    this.SetField(room, client);
                                }
                            }
                            break;
                        case "live":
                            {
                                String room = values.Last() as String;
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
                        case "nick":
                            {
                                String name = values[1] as String;
                                if (name.Length > 1)
                                {
                                    String room = values.Last() as String;
                                    User usr = this.FindUser(room, client);
                                    if (usr != null)
                                    {
                                        usr.SetName(name);
                                        this.UpdateUsers(room);
                                    }
                                }
                            }
                            break;
                        case "mclk":
                            {
                                String room = values.Last() as String;
                                data = JsonConvert.SerializeObject(values);
                                this.roomshistory[room].Add(data);
                                this.SendForAll(data, room);
                            }
                            break;
                        case "dlin":
                            {
                                String room = values.Last() as String;
                                data = JsonConvert.SerializeObject(values);
                                this.roomshistory[room].Add(data);
                                this.SendForAll(data, room);
                            }
                            break;
                        case "clca":
                            {
                                String room = values.Last() as String;
                                data = JsonConvert.SerializeObject(values);
                                this.roomshistory[room].Clear();
                                this.SendForAll(data, room);
                            }
                            break;
                    }
                }
            }
            catch (Exception err)
            {
                Console.WriteLine(err);
                return;
            }
        }
    }
}