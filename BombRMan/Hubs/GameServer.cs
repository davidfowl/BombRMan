using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using SignalR;
using SignalR.Hubs;

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
        private const int DELTA = 10;
        private const int FPS = 60;

        public Task Connect()
        {
            Caller.initializeMap(mapData).Wait();

            EnsureGameLoop();

            Player player;
            if (_availablePlayers.TryPop(out player))
            {
                _activePlayers.TryAdd(Context.ConnectionId, new PlayerState
                {
                    Inputs = new ConcurrentQueue<KeyboardState>(),
                    Player = player
                });

                Caller.initializePlayer(player).Wait();
            }

            return Clients.initialize(_activePlayers.Values.Select(v => v.Player));
        }

        private static void EnsureGameLoop()
        {
            if (Interlocked.Exchange(ref _gameLoopRunning, 1) == 0)
            {
                new Thread(_ => RunGameLoop()).Start();
            }
        }

        public Task Reconnect(IEnumerable<string> groups)
        {
            EnsureGameLoop();

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
                    ExactY = _initialPositions[i].Y * POWER,
                    Direction = Direction.SOUTH
                });
            }

            return stack;
        }

        public void SendKeys(KeyboardState[] inputs)
        {
            PlayerState state;
            if (_activePlayers.TryGetValue(Context.ConnectionId, out state))
            {
                lock (state)
                {
                    foreach (var input in inputs)
                    {
                        state.Inputs.Enqueue(input);
                    }
                }
            }
        }

        public static void RunGameLoop()
        {
            var frameTicks = (int)Math.Round(1000.0 / FPS);
            var context = GlobalHost.ConnectionManager.GetHubContext<GameServer>();
            var lastUpdate = 0;

            while (true)
            {
                int delta = (lastUpdate + frameTicks) - Environment.TickCount;
                if (delta < 0)
                {
                    lastUpdate = Environment.TickCount;

                    Update(context);
                }
                else
                {
                    Thread.Sleep(TimeSpan.FromTicks(delta));
                }
            }
        }

        private static void Update(IHubContext context)
        {
            foreach (var pair in _activePlayers)
            {
                KeyboardState input;
                if (pair.Value.Inputs.TryDequeue(out input))
                {
                    pair.Value.Player.Update(input);
                    context.Clients.updatePlayerState(pair.Value.Player);
                }
            }
        }

        public Task Disconnect()
        {
            PlayerState state;
            if (_activePlayers.TryRemove(Context.ConnectionId, out state))
            {
                Clients.playerLeft(state.Player);

                Point pos = _initialPositions[state.Player.Index];
                _availablePlayers.Push(new Player
                {
                    Index = state.Player.Index,
                    X = pos.X,
                    Y = pos.Y,
                    ExactX = pos.X * POWER,
                    ExactY = pos.Y * POWER,
                    Direction = Direction.SOUTH
                });
            }

            return null;
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
            private static readonly Point[] EastTargets = new Point[] { new Point(1, -1), new Point(1, 0), new Point(1, 1) };
            private static readonly Point[] WestTargets = new Point[] { new Point(-1, -1), new Point(-1, 0), new Point(-1, 1) };
            private static readonly Point[] NorthTargets = new Point[] { new Point(-1, -1), new Point(0, -1), new Point(1, -1) };
            private static readonly Point[] SouthTargets = new Point[] { new Point(-1, 1), new Point(0, 1), new Point(1, 1) };

            public int X { get; set; }
            public int Y { get; set; }
            public int ExactX { get; set; }
            public int ExactY { get; set; }
            public int DirectionX { get; set; }
            public int DirectionY { get; set; }

            public Direction Direction { get; set; }

            public int Index { get; set; }

            public int LastProcessed { get; set; }

            public void Update(KeyboardState input)
            {
                LastProcessed = input.Id;

                int x = ExactX,
                    y = ExactY;

                if (!input[Keys.UP])
                {
                    DirectionY = 0;
                }

                if (!input[Keys.DOWN])
                {
                    DirectionY = 0;
                }

                if (!input[Keys.LEFT])
                {
                    DirectionX = 0;
                }

                if (!input[Keys.RIGHT])
                {
                    DirectionX = 0;
                }

                if (input[Keys.DOWN])
                {
                    DirectionY = 1;
                }

                if (input[Keys.UP])
                {
                    DirectionY = -1;
                }

                if (input[Keys.LEFT])
                {
                    DirectionX = -1;
                }

                if (input[Keys.RIGHT])
                {
                    DirectionX = 1;
                }

                SetDirection(DirectionX, DirectionY);

                x += DirectionX * DELTA;
                y += DirectionY * DELTA;

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
                var collisions = new List<Point>();
                var possible = new List<Point>();

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

                if (collisions.Count == 0)
                {
                    SetDirection(DirectionX, DirectionY);

                    X = actualX;
                    Y = actualY;

                    ExactX = x;
                    ExactY = y;
                }
                else
                {
                    var candidates = new List<Tuple<int, int, Point>>();
                    Tuple<int, int, Point> candidate = null;
                    var p1 = new Point(actualX + DirectionX, actualY);
                    var p2 = new Point(actualX, actualY + DirectionY);
                    foreach (var nextMove in possible)
                    {
                        if (p1.Equals(nextMove))
                        {
                            candidates.Add(Tuple.Create(DirectionX, 0, p1));
                        }

                        if (p2.Equals(nextMove))
                        {
                            candidates.Add(Tuple.Create(0, DirectionY, p2));
                        }
                    }

                    if (candidates.Count == 1)
                    {
                        candidate = candidates[0];
                    }
                    else if (candidates.Count == 2)
                    {
                        int minDistance = Int32.MaxValue;
                        for (int i = 0; i < candidates.Count; ++i)
                        {
                            var targetCandidate = candidates[i];
                            int xs = (ExactX - candidates[i].Item3.X * POWER);
                            int ys = (ExactY - candidates[i].Item3.Y * POWER);
                            int distance = xs * xs + ys * ys;

                            if (distance < minDistance)
                            {
                                minDistance = distance;
                                candidate = targetCandidate;
                            }
                        }
                    }

                    if (candidate != null)
                    {
                        var diffX = candidate.Item3.X * POWER - ExactX;
                        var diffY = candidate.Item3.Y * POWER - ExactY;
                        var absX = Math.Abs(diffX);
                        var absY = Math.Abs(diffY);
                        int effectiveDirectionX = 0;
                        int effectiveDirectionY = 0;

                        if (absX == 100)
                        {
                            effectiveDirectionX = 0;
                        }
                        else
                        {
                            effectiveDirectionX = Math.Sign(diffX);
                        }

                        if (absY == 100)
                        {
                            effectiveDirectionY = 0;
                        }
                        else
                        {
                            effectiveDirectionY = Math.Sign(diffY);
                        }

                        if (effectiveDirectionX == 0 && effectiveDirectionY == 0)
                        {
                            effectiveDirectionX = candidate.Item1;
                            effectiveDirectionY = candidate.Item2;
                        }

                        SetDirection(effectiveDirectionX, effectiveDirectionY);

                        ExactX += DELTA * effectiveDirectionX;
                        X = actualX;

                        ExactY += DELTA * effectiveDirectionY;
                        Y = actualY;
                    }
                    else
                    {
                        var diffY = (collisions[0].Y * POWER - ExactY);
                        var diffX = (collisions[0].X * POWER - ExactX);
                        var absX = Math.Abs(diffX);
                        var absY = Math.Abs(diffY);
                        int effectiveDirectionX = 0;
                        int effectiveDirectionY = 0;

                        if (absX >= 35 && absX < 100)
                        {
                            effectiveDirectionX = -Math.Sign(diffX);
                        }

                        if (absY >= 35 && absY < 100)
                        {
                            effectiveDirectionY = -Math.Sign(diffY);
                        }

                        SetDirection(effectiveDirectionX, effectiveDirectionY);

                        ExactX += DELTA * effectiveDirectionX;
                        ExactY += DELTA * effectiveDirectionY;
                    }
                }
            }

            private void SetDirection(int x, int y)
            {
                if (x == -1)
                {
                    Direction = GameServer.Direction.WEST;
                }
                else if (x == 1)
                {
                    Direction = GameServer.Direction.EAST;
                }

                if (y == -1)
                {
                    Direction = GameServer.Direction.NORTH;
                }
                else if (y == 1)
                {
                    Direction = GameServer.Direction.SOUTH;
                }
            }

            private Point[] GetHitTargets()
            {
                return GetXHitTargets().Concat(GetYHitTargets()).ToArray();
            }

            private Point[] GetXHitTargets()
            {
                if (DirectionX == 1)
                {
                    return EastTargets;
                }
                else if (DirectionX == -1)
                {
                    return WestTargets;
                }

                return new Point[0];
            }

            private Point[] GetYHitTargets()
            {
                if (DirectionY == -1)
                {
                    return NorthTargets;
                }
                else if (DirectionY == 1)
                {
                    return SouthTargets;
                }

                return new Point[0];
            }
        }

        public enum Direction
        {
            NORTH,
            SOUTH,
            EAST,
            WEST
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

        public class PlayerState
        {
            public ConcurrentQueue<KeyboardState> Inputs { get; set; }
            public Player Player { get; set; }
        }

        public class KeyboardState
        {
            private readonly Dictionary<Keys, bool> _keyState;
            public int Id { get; private set; }

            public KeyboardState(Dictionary<Keys, bool> keyState, int id)
            {
                _keyState = keyState;
                Id = id;
            }

            public bool this[Keys key]
            {
                get
                {
                    return _keyState[key];
                }
            }

            public bool Empty
            {
                get
                {
                    return !this[Keys.A] &&
                           !this[Keys.D] &&
                           !this[Keys.DOWN] &&
                           !this[Keys.LEFT] &&
                           !this[Keys.RIGHT] &&
                           !this[Keys.UP] &&
                           !this[Keys.P];
                }
            }

            public override string ToString()
            {
                var sb = new StringBuilder();
                foreach (var value in Enum.GetValues(typeof(Keys)))
                {
                    if (sb.Length > 0)
                    {
                        sb.Append(", ");
                    }

                    sb.Append(Enum.GetName(typeof(Keys), value))
                      .Append(" = ")
                      .Append(this[(Keys)value]);
                }

                sb.AppendLine();

                return sb.ToString();
            }
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
    }
}