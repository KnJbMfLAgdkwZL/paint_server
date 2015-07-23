using System;
namespace Server
{
    class Program
    {
        static void Main(string[] args)
        {
            /*
            AsyncWebSocketsServer server = new AsyncWebSocketsServer();
            server.Start(6789);
            String str = "";
            while (true)
            {
                str = Console.ReadLine();
                str = str.ToLower();
                if (str == "stop")
                {
                    server.Stop();
                    break;
                }
            }
             * */

            Server_2 srv = new Server_2();
            srv.Start(6789);

            Console.ReadLine();


        }
    }
}