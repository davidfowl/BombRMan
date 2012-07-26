using System;
using System.Diagnostics;
using System.Threading;
using BombRMan.Hubs;
using SignalR.Hosting.Self;

namespace BombRMan.SelfHost
{
    class Program
    {
        static void Main(string[] args)
        {
            Debug.Listeners.Add(new ConsoleTraceListener());
            Debug.AutoFlush = true;

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
