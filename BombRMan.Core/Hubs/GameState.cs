using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
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

        public GameState(IHubContext<GameServer> hubContext, IHostApplicationLifetime hostApplicationLifetime)
        {
            _hubContext = hubContext;
            _hostApplicationLifetime = hostApplicationLifetime;

            new Thread(_ => RunGameLoop()).Start();

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
                lock (state)
                {
                    foreach (var input in inputs)
                    {
                        state.Inputs.Enqueue(input);
                    }
                }
            }
        }

        public void RunGameLoop()
        {
            var frameTicks = (int)Math.Round(1000.0 / FPS);
            var lastUpdate = 0;

            while (!_hostApplicationLifetime.ApplicationStopping.IsCancellationRequested)
            {
                int delta = (lastUpdate + frameTicks) - Environment.TickCount;
                if (delta < 0)
                {
                    lastUpdate = Environment.TickCount;

                    Update();
                }
                else
                {
                    Thread.Sleep(delta);
                }
            }
        }

        private void Update()
        {
            foreach (var pair in _activePlayers)
            {
                if (pair.Value.Inputs.TryDequeue(out KeyboardState input))
                {
                    pair.Value.Player.Update(input);
                    _ = _hubContext.Clients.All.SendAsync("updatePlayerState", pair.Value.Player);
                }
            }
        }
    }
}
