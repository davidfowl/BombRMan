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
using System.Diagnostics;

namespace BombRMan.Hubs
{
    public class GameServer : Hub, IConnected, IDisconnect
    {
        static string mapData = "222222222222222" +
                                "200000000000002" +
                                "202020202020202" +
                                "200000000000002" +
                                "202020202020202" +
                                "200000000000002" +
                                "202020202020202" +
                                "200000000000002" +
                                "202020202020202" +
                                "200000000000002" +
                                "202020202020202" +
                                "200000000000002" +
                                "222222222222222";

        private static Point[] _initialPositions;
        private static ConcurrentStack<Player> _availablePlayers = GetPlayers();
        private static ConcurrentDictionary<string, PlayerState> _activePlayers = new ConcurrentDictionary<string, PlayerState>();
        private static int _gameLoopRunning;
        private static Map _map = new Map(mapData, 15, 13, 32);
        private const int POWER = 100;

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
                    Inputs = new LimitedQueue<Dictionary<Keys, bool>>(10),
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
                    Y = _initialPositions[i].Y,
                    ExactX = _initialPositions[i].X * POWER,
                    ExactY = _initialPositions[i].Y * POWER
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


        private static bool Movable(int x, int y)
        {
            if (y >= 0 && y < _map.Height && x >= 0 && x < _map.Width)
            {
                if (_map[x, y] == Tile.GRASS)
                {
                    return true;
                }
            }

            return false;
        }

        public class Player
        {
            private int _directionX;
            private int _directionY;

            public int X { get; set; }
            public int Y { get; set; }
            public int ExactX { get; set; }
            public int ExactY { get; set; }

            public int Direction { get; set; }

            public int Index { get; set; }

            public void HandleInput(Dictionary<Keys, bool> input)
            {
                int x = ExactX,
                    y = ExactY;

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

                x += _directionX * 10;
                y += _directionY * 10;

                MoveExact(x, y);
            }

            private void MoveExact(int x, int y)
            {
                float effectiveX = x / (POWER * 1f),
                      effectiveY = y / (POWER * 1f);

                var actualX = (int)Math.Floor((float)(x + (POWER / 2)) / POWER);
                var actualY = (int)Math.Floor((float)(y + (POWER / 2)) / POWER);
                var targets = GetHitTargets();
                var sourceLeft = effectiveX * _map.TileSize;
                var sourceTop = effectiveY * _map.TileSize;
                var sourceRect = new RectangleF(sourceLeft, sourceTop, _map.TileSize, _map.TileSize);
                List<Point> collisions = new List<Point>();
                List<Point> possible = new List<Point>();

                foreach (var t in targets)
                {
                    int targetX = actualX + t.X,
                        targetY = actualY + t.Y;

                    var targetRect = new RectangleF(targetX * _map.TileSize, targetY * _map.TileSize, _map.TileSize, _map.TileSize);
                    var movable = Movable(targetX, targetY);
                    var intersects = sourceRect.IntersectsWith(targetRect);

                    if (!movable && intersects)
                    {
                        collisions.Add(new Point(targetX, targetY));
                    }
                    else
                    {
                        possible.Add(new Point(targetX, targetY));
                    }
                }

                Debug.WriteLine("DATA");
                Debug.WriteLine("{0}, {1}", effectiveX, effectiveY);
                Debug.WriteLine("{0}, {1}", actualX, actualY);

                Debug.WriteLine("COLLISIONS");
                foreach (var c in collisions)
                {
                    Debug.WriteLine(c);
                }

                Debug.WriteLine("POSSIBLE");
                foreach (var p in possible)
                {
                    Debug.WriteLine(p);
                }

                if (collisions.Count == 0)
                {
                    X = actualX;
                    Y = actualY;

                    ExactX = x;
                    ExactY = y;
                }
                else
                {

                }

                //var sourceLeft = this.effectiveX * game.map.tileSize,
                //var sourceTop = this.effectiveY * game.map.tileSize,
                //var sourceRect = new RectangleF()
                //{
                //    left = sourceLeft,
                //    top = sourceTop,
                //    right = sourceLeft + game.map.tileSize,
                //    bottom = sourceTop + game.map.tileSize
                //};
            }

            private Point[] GetHitTargets()
            {
                return GetXHitTargets().Concat(GetYHitTargets()).ToArray();
            }

            private Point[] GetXHitTargets()
            {
                if (_directionX == 1)
                {
                    return new Point[] { new Point(1, -1), new Point(1, 0), new Point(1, 1) };
                }
                else if (_directionX == -1)
                {
                    return new Point[] { new Point(-1, -1), new Point(-1, 0), new Point(-1, 1) };
                }

                return new Point[0];
            }

            private Point[] GetYHitTargets()
            {
                if (_directionY == -1)
                {
                    return new Point[] { new Point(-1, -1), new Point(0, -1), new Point(1, -1) };
                }
                else if (_directionY == 1)
                {
                    return new Point[] { new Point(-1, 1), new Point(0, 1), new Point(1, 1) };
                }

                return new Point[0];
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
            var interval = TimeSpan.FromMilliseconds(15);
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
                    Y = pos.Y,
                    ExactX = pos.X * POWER,
                    ExactY = pos.Y * POWER,
                });
            }

            return null;
        }

        public class Map
        {
            private readonly Tile[] _map;

            public Map(string map, int width, int height, int tileSize)
            {
                _map = Create(map);
                Width = width;
                Height = height;
                TileSize = tileSize;
            }

            private Tile[] Create(string map)
            {
                var tiles = new Tile[map.Length];
                for (int i = 0; i < map.Length; i++)
                {
                    tiles[i] = (Tile)((int)map[i] - '0');
                }
                return tiles;
            }

            public Tile this[int x, int y]
            {
                get
                {
                    return _map[GetIndex(x, y)];
                }
                set
                {
                    _map[GetIndex(x, y)] = value;
                }
            }
            private int GetIndex(int x, int y)
            {
                return (y * Width) + x;
            }

            public int TileSize { get; private set; }
            public int Width { get; private set; }
            public int Height { get; private set; }
        }

        public enum Tile
        {
            GRASS = 0,
            WALL = 2,
            BRICK = 3,
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