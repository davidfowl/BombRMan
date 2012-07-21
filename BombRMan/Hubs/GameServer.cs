using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Drawing;
using System.Threading.Tasks;
using SignalR.Hubs;

namespace BombRMan.Hubs
{
    public class GameServer : Hub, IConnected
    {
        string mapData = "222222222222222" +
                         "200003030300002" +
                         "202323232323202" +
                         "203333333333302" +
                         "202323232323202" +
                         "233333333333332" +
                         "232323232323232" +
                         "233333333333332" +
                         "202323232323202" +
                         "203333333333302" +
                         "202323232323202" +
                         "200003030300002" +
                         "222222222222222";

        private static ConcurrentQueue<Player> _availablePlayers = GetPlayers();
        private static ConcurrentBag<Player> _activePlayers = new ConcurrentBag<Player>();

        public Task Connect()
        {
            Caller.initializeMap(mapData).Wait();

            Player player;
            if (_availablePlayers.TryDequeue(out player))
            {
                _activePlayers.Add(player);

                Caller.initializePlayer(player).Wait();
            }

            return Clients.initialize(_activePlayers.ToArray());
        }

        public Task Reconnect(IEnumerable<string> groups)
        {
            return null;
        }

        private static ConcurrentQueue<Player> GetPlayers()
        {
            var queue = new ConcurrentQueue<Player>();
            queue.Enqueue(new Player { Index = 1, X = 1, Y = 1 });
            queue.Enqueue(new Player { Index = 2, X = 13, Y = 1 });
            queue.Enqueue(new Player { Index = 3, X = 1, Y = 11 });
            queue.Enqueue(new Player { Index = 4, X = 13, Y = 11 });
            return queue;
        }

        public class Player
        {
            public int X { get; set; }
            public int Y { get; set; }
            public int Index { get; set; }
        }
    }
}