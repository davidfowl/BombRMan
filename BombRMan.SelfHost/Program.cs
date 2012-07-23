using System;
using SignalR.Hosting.Self;

namespace BombRMan.SelfHost
{
    class Program
    {
        static void Main(string[] args)
        {
            var server = new Server("http://localhost:8081/");
            server.MapHubs();

            server.Start();
            Console.WriteLine("Server running");

            while (true)
            {
                ConsoleKeyInfo ki = Console.ReadKey(false);
                if (ki.Key == ConsoleKey.Escape)
                {
                    break;
                }
            }
        }
    }
}
