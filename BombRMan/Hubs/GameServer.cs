using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using SignalR;
using SignalR.Hubs;

namespace BombRMan.Hubs
{
    public class GameServer : Hub, IConnected, IDisconnect
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

        private static Point[] _initialPositions;
        private static ConcurrentStack<Player> _availablePlayers = GetPlayers();
        private static ConcurrentDictionary<string, PlayerState> _activePlayers = new ConcurrentDictionary<string, PlayerState>();
        private static int _gameLoopRunning;

        public Task Connect()
        {
            Caller.initializeMap(mapData).Wait();

            if (Interlocked.Exchange(ref _gameLoopRunning, 1) == 0)
            {
                new Thread(_ => StartGameLoop()).Start();
                new Thread(_ => StartUpdateLoop()).Start();
            }

            Player player;
            if (_availablePlayers.TryPop(out player))
            {
                _activePlayers.TryAdd(Context.ConnectionId, new PlayerState
                {
                    Inputs = new LimitedQueue<Dictionary<Keys, bool>>(120),
                    Player = player
                });

                Caller.initializePlayer(player).Wait();
            }

            return Clients.initialize(_activePlayers.Values.Select(v => v.Player));
        }

        public Task Reconnect(IEnumerable<string> groups)
        {
            return null;
        }

        private static ConcurrentStack<Player> GetPlayers()
        {
            var stack = new ConcurrentStack<Player>();
            _initialPositions = new Point[4];
            _initialPositions[0] = new Point(1, 1);
            _initialPositions[1] = new Point(13, 1);
            _initialPositions[2] = new Point(1, 11);
            _initialPositions[3] = new Point(13, 11);

            for (int i = _initialPositions.Length - 1; i >= 0; i--)
            {
                stack.Push(new Player
                {
                    Index = i,
                    X = _initialPositions[i].X,
                    Y = _initialPositions[i].Y
                });
            }
            return stack;
        }

        public void SendKeys(JObject keyState)
        {
            PlayerState state;
            if (_activePlayers.TryGetValue(Context.ConnectionId, out state))
            {
                state.Inputs.Enqueue(keyState.ToObject<Dictionary<Keys, bool>>());
            }
        }

        public class Player
        {
            private int _directionX;
            private int _directionY;

            public double X { get; set; }
            public double Y { get; set; }
            public int Direction { get; set; }

            public int Index { get; set; }

            public void HandleInput(Dictionary<Keys, bool> input)
            {
                if (!input[Keys.UP])
                {
                    _directionY = 0;
                }

                if (!input[Keys.DOWN])
                {
                    _directionY = 0;
                }

                if (!input[Keys.LEFT])
                {
                    _directionX = 0;
                }

                if (!input[Keys.RIGHT])
                {
                    _directionX = 0;
                }

                if (input[Keys.DOWN])
                {
                    _directionY = 1;
                }

                if (input[Keys.UP])
                {
                    _directionY = -1;
                }

                if (input[Keys.LEFT])
                {
                    _directionX = -1;
                }

                if (input[Keys.RIGHT])
                {
                    _directionX = 1;
                }

                X += _directionX * 0.1;
                Y += _directionY * 0.1;
            }
        }

        public enum Keys
        {
            UP = 38,
            DOWN = 40,
            LEFT = 37,
            RIGHT = 39,
            A = 65,
            D = 68,
            P = 80
        }


        private void StartUpdateLoop()
        {
            var context = GlobalHost.ConnectionManager.GetHubContext<GameServer>();
            var interval = TimeSpan.FromMilliseconds(45);
            while (true)
            {
                foreach (var pair in _activePlayers)
                {
                    context.Clients.updatePlayerState(pair.Value.Player);
                }

                Thread.Sleep(interval);
            }
        }

        private static void StartGameLoop()
        {
            var interval = TimeSpan.FromMilliseconds(1000.0 / 60);
            while (true)
            {
                foreach (var pair in _activePlayers)
                {
                    Dictionary<Keys, bool> inputState;
                    if (pair.Value.Inputs.TryDequeue(out inputState))
                    {
                        pair.Value.Player.HandleInput(inputState);
                    }
                }

                Thread.Sleep(interval);
            }
        }


        public class PlayerState
        {
            public LimitedQueue<Dictionary<Keys, bool>> Inputs { get; set; }
            public Player Player { get; set; }
        }

        public Task Disconnect()
        {
            PlayerState state;
            if (_activePlayers.TryRemove(Context.ConnectionId, out state))
            {
                Point pos = _initialPositions[state.Player.Index];
                _availablePlayers.Push(new Player
                {
                    Index = state.Player.Index,
                    X = pos.X,
                    Y = pos.Y
                });
            }

            return null;
        }

        public class LimitedQueue<T> : ConcurrentQueue<T>
        {
            public int Limit
            {
                get;
                set;
            }

            public LimitedQueue(int limit)
            {
                Limit = limit;
            }

            public new void Enqueue(T item)
            {
                T element;
                if (Count >= Limit && TryDequeue(out element))
                {
                }
                base.Enqueue(item);
            }
        }
    }
}