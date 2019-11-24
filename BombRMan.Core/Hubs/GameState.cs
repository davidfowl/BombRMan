using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Hosting;

namespace BombRMan.Hubs
{
    public class GameState
    {
        public const int POWER = 100;
        public const int DELTA = 10;
        public const int FPS = 60;

        static string _mapData = "222222222222222" +
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

        private readonly Point[] _initialPositions;
        private readonly ConcurrentStack<Player> _availablePlayers = new ConcurrentStack<Player>();
        private readonly ConcurrentDictionary<string, PlayerState> _activePlayers = new ConcurrentDictionary<string, PlayerState>();
        private readonly Map _map = new Map(_mapData, 15, 13, 32);
        private readonly IHubContext<GameServer> _hubContext;
        private readonly IHostApplicationLifetime _hostApplicationLifetime;
        private int _updatesPerSecond;
        private int _inputsPerSecond;

        public GameState(IHubContext<GameServer> hubContext, IHostApplicationLifetime hostApplicationLifetime)
        {
            _hubContext = hubContext;
            _hostApplicationLifetime = hostApplicationLifetime;

            var gameLoopThread = new Thread(_ => RunGameLoop());
            gameLoopThread.IsBackground = true;
            gameLoopThread.Start();

            _initialPositions = new Point[4];
            _initialPositions[0] = new Point(1, 1);
            _initialPositions[1] = new Point(13, 1);
            _initialPositions[2] = new Point(1, 11);
            _initialPositions[3] = new Point(13, 11);

            for (int i = _initialPositions.Length - 1; i >= 0; i--)
            {
                _availablePlayers.Push(new Player
                {
                    Index = i,
                    X = _initialPositions[i].X,
                    Y = _initialPositions[i].Y,
                    ExactX = _initialPositions[i].X * POWER,
                    ExactY = _initialPositions[i].Y * POWER,
                    Direction = Direction.SOUTH,
                    GameState = this
                });
            }

            var timer = new Timer(OnTick, null, 1000, 1000);
        }

        public Map Map => _map;

        public IEnumerable<Player> ActivePlayers => _activePlayers.Select(a => a.Value.Player);

        public bool TryAddPlayer(string playerId, out Player player)
        {
            if (_availablePlayers.TryPop(out player))
            {
                _activePlayers.TryAdd(playerId, new PlayerState
                {
                    Inputs = new ConcurrentQueue<KeyboardState>(),
                    Player = player,
                });

                return true;
            }

            return false;
        }

        public bool TryRemovePlayer(string playerId, out Player player)
        {
            if (_activePlayers.TryRemove(playerId, out var state))
            {
                player = state.Player;
                Point pos = _initialPositions[state.Player.Index];
                _availablePlayers.Push(new Player
                {
                    Index = state.Player.Index,
                    X = pos.X,
                    Y = pos.Y,
                    ExactX = pos.X * POWER,
                    ExactY = pos.Y * POWER,
                    Direction = Direction.SOUTH,
                    GameState = this
                });

                return true;
            }
            player = null;
            return false;
        }

        public void SendKeys(string playerId, KeyboardState[] inputs)
        {
            PlayerState state;
            if (_activePlayers.TryGetValue(playerId, out state))
            {
                foreach (var input in inputs)
                {
                    state.Inputs.Enqueue(input);
                }
            }
        }

        private void OnTick(object state)
        {
            var updates = Interlocked.Exchange(ref _updatesPerSecond, 0);
            var inputs = Interlocked.Exchange(ref _inputsPerSecond, 0);
            _hubContext.Clients.All.SendAsync("serverStats", new { Updates = updates, ProcessedInputs = inputs });
        }

        public void RunGameLoop()
        {
            long lastMs = 0;
            var sw = Stopwatch.StartNew();
            long lastFpsCheck = 0;
            var actualFps = 0;

            while (!_hostApplicationLifetime.ApplicationStopping.IsCancellationRequested)
            {
                var frameMs = (int)Math.Round(1000.0 / FPS);
                long delta = (lastMs + frameMs) - sw.ElapsedMilliseconds;

                // Actual FPS check, update every second
                if ((lastFpsCheck + 1000 - sw.ElapsedMilliseconds) <= 0)
                {
                    lastFpsCheck = sw.ElapsedMilliseconds;
                    actualFps = 0;
                }

                if (delta <= 0)
                {
                    actualFps++;
                    Update();
                    lastMs = sw.ElapsedMilliseconds;
                }
                else
                {
                    Thread.Yield();
                }
            }
        }

        private void Update()
        {
            Interlocked.Increment(ref _updatesPerSecond);

            foreach (var pair in _activePlayers)
            {
                if (pair.Value.Inputs.TryDequeue(out KeyboardState input))
                {
                    pair.Value.Player.Update(input);
                    _ = _hubContext.Clients.All.SendAsync("updatePlayerState", pair.Value.Player);
                }
                Interlocked.Increment(ref _inputsPerSecond);
            }
        }
    }
}
