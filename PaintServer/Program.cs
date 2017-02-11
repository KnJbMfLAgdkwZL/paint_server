using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
namespace PaintServer
{
    class Program
    {
        static void Main(string[] args)
        {
            Server server = new Server();
            server.Start(6780);

            Console.ReadLine();

            server.Stop();
        }
    }
}