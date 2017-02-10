using System;
namespace Server
{
    class Program
    {
        static void Main(string[] args)
        {
            AsyncWebSocketsServer srv = new AsyncWebSocketsServer();
            srv.Start();
            Console.ReadLine();
        }
    }
}