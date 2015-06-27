using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Collections;
using System.Threading;
using System.Net;
using System.Net.Sockets;
using System.Text.RegularExpressions;
using System.Security.Cryptography;
using System.Net.WebSockets;
namespace Server
{
    class MyServer
    {
        private Socket listener = null;
        public MyServer(int port)
        {
            try
            {
                //ProtocolType.Tcp
                this.listener = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.IP);
                listener.Bind(new IPEndPoint(IPAddress.Any, port));
                listener.Listen(10);
            }
            catch (Exception err)
            {
                Console.WriteLine(err);
            }
        }
        public void Start()
        {
            try
            {
                Thread S_Thread = new Thread(this.GetConnect);
                S_Thread.IsBackground = true;
                S_Thread.Start();
            }
            catch (Exception err)
            {
                Console.WriteLine(err);
            }
        }
        private void GetConnect()
        {
            try
            {
                while (true)
                {
                    Socket client = this.listener.Accept();
                    Thread S_Thread = new Thread(this.ServerLogic);
                    S_Thread.IsBackground = true;
                    S_Thread.Start(client);
                }
            }
            catch (Exception err)
            {
                Console.WriteLine(err);
            }
        }
        private void ServerLogic(object obj)
        {
            try
            {
                Socket client = obj as Socket;
                byte[] MASK = new byte[4];
                while (true)
                {
                    byte[] Buffer = new byte[1024];
                    int len = client.Receive(Buffer);
                    if (len > 0)
                    {
                        string data = Encoding.UTF8.GetString(Buffer, 0, len);
                        if (new Regex("^GET").IsMatch(data))
                        {
                            Console.WriteLine(data);
                            //
                            string key = new Regex("Sec-WebSocket-Key: (.*)").Match(data).Groups[1].Value.Trim();
                            MASK = SHA1.Create().ComputeHash(
                                Encoding.UTF8.GetBytes(key + "258EAFA5-E914-47DA-95CA-C5AB0DC85B11")
                            );
                            //
                            string str = "";
                            str += "HTTP/1.1 101 Switching Protocols" + Environment.NewLine;
                            str += "Connection: Upgrade" + Environment.NewLine;
                            str += "Upgrade: websocket" + Environment.NewLine;
                            str += "Sec-WebSocket-Accept: ";
                            str += Convert.ToBase64String(MASK);
                            str += Environment.NewLine;
                            str += Environment.NewLine;

                            this.SendStr(client, str);
                        }
                        else
                        {
                            Buffer = this.DecodeMessag(Buffer, len);
                            string str = this.BufferToStr(Buffer);
                            Console.WriteLine(str);
                            this.SendStrForWbebSocet(client, "This string from C# server Hello World");
                        }
                    }
                }
            }
            catch (Exception err)
            {
                Console.WriteLine(err);
            }
        }
        private Byte[] DecodeMessag(Byte[] Buffer, int len)
        {
            Byte[] decoded = new Byte[len - 2 - 4];
            Byte[] encoded = new Byte[len - 2 - 4];
            Byte[] key = new Byte[4];
            for (int i = 0; i < len; i++)
            {
                if (i > 1 && i < 6)
                {
                    key[i - 2] = Buffer[i];
                }
                if (i >= 6)
                {
                    encoded[i - 6] = Buffer[i];
                }
            }
            for (int i = 0; i < encoded.Length; i++)
            {
                decoded[i] = (Byte)(encoded[i] ^ key[i % 4]);
            }
            return decoded;
        }
        private string BufferToStr(Byte[] buffer)
        {
            string str = "";
            for (int i = 0; i < buffer.Length; i++)
            {
                str += (char)buffer[i];
            }
            return str;
        }
        private void SendStr(Socket client, string str)
        {
            byte[] sendbuf = Encoding.UTF8.GetBytes(str);
            client.Send(sendbuf);
        }
        private void SendStrForWbebSocet(Socket client, string str)
        {
            List<byte> buff = new List<byte>();
            buff.Add(0x81);
            buff.Add(Convert.ToByte(str.Length));
            buff.AddRange(Encoding.UTF8.GetBytes(str));
            client.Send(buff.ToArray());
        }
    }
    class Program
    {
        static void Main(string[] args)
        {
            /*
                Google Chrome (начиная с версии 4.0.249.0);
                Apple Safari (начиная с версии 5.0.7533.16);
                Mozilla Firefox (начиная с версии 4);
                Opera (начиная с версии 10.70 9067);
                Internet Explorer (начиная с версии 10);             
            */
            MyServer server = new MyServer(7890);
            server.Start();

            Console.ReadLine();
        }
    }
}