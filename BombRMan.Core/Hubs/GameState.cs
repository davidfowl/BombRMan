using System.Buffers;
using System.Collections.Immutable;
using System.Collections.Concurrent;
using System.Drawing;
using System.Diagnostics;
using Microsoft.AspNetCore.SignalR;

namespace BombRMan.Hubs;

public class GameState
{
    public const int POWER = 100;
    public const int DELTA = 10;
    public const int FPS = 60;
    private static readonly long InputTimeoutTicks = Stopwatch.Frequency / 4;

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
    private readonly ConcurrentStack<Player> _availablePlayers = new();
    private readonly PlayerList _activePlayers = new();
    private readonly Map _map = new(_mapData, 15, 13, 32);
    private readonly IHubContext<GameServer> _hubContext;
    private readonly IHostApplicationLifetime _hostApplicationLifetime;
    private int _updatesPerSecond;
    private int _inputsPerSecond;

    public GameState(IHubContext<GameServer> hubContext, IHostApplicationLifetime hostApplicationLifetime)
    {
        _hubContext = hubContext;
        _hostApplicationLifetime = hostApplicationLifetime;

        var gameLoopThread = new Thread(_ => RunGameLoop())
        {
            IsBackground = true
        };

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

        Task.Run(ServerStatsTimer);
    }

    private async Task ServerStatsTimer()
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(1));

        // Reuse this for stats
        var serverStats = new ServerStats();

        while (await timer.WaitForNextTickAsync())
        {
            var updates = Interlocked.Exchange(ref _updatesPerSecond, 0);
            var inputs = Interlocked.Exchange(ref _inputsPerSecond, 0);

            serverStats.Updates = updates;
            serverStats.ProcessedInputs = inputs;

            await _hubContext.Clients.All.SendAsync("serverStats", serverStats);
        }
    }

    public Map Map => _map;

    public ImmutableArray<Player> ActivePlayers => _activePlayers.Players;

    public bool TryAddPlayer(string playerId, out Player player)
    {
        if (_availablePlayers.TryPop(out player))
        {
            _activePlayers.Add(new PlayerState
            {
                PlayerId = playerId,
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
            state.Dispose();
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
        if (_activePlayers.TryGet(playerId, out var state))
        {
            foreach (var input in inputs)
            {
                if (input.KeyState is null) break;

                if (!state.Inputs.TryAccept(input, Stopwatch.GetTimestamp()))
                {
                    input.Dispose();
                }
            }
        }
        else
        {
            foreach (var input in inputs)
            {
                if (input.KeyState is null) break;
                input.Dispose();
            }
        }

        // Return the batch to the pool, clear the array so we can use null to figure out what
        // the last entry is without storing a struct on the heap
        ArrayPool<KeyboardState>.Shared.Return(inputs, clearArray: true);
    }

    public void RunGameLoop()
    {
        long lastUpdate = Stopwatch.GetTimestamp();
        long accumulatedTicks = 0;

        while (!_hostApplicationLifetime.ApplicationStopping.IsCancellationRequested)
        {
            long update = Stopwatch.GetTimestamp();
            accumulatedTicks += (update - lastUpdate) * FPS;
            lastUpdate = update;

            while (accumulatedTicks >= Stopwatch.Frequency)
            {
                accumulatedTicks -= Stopwatch.Frequency;
                Update();
            }

            Thread.Sleep(1);
        }
    }

    private void Update()
    {
        Interlocked.Increment(ref _updatesPerSecond);
        var now = Stopwatch.GetTimestamp();

        foreach (var state in _activePlayers.PlayerStates)
        {
            if (state.Inputs.TryTakeLatest(out var input))
            {
                state.Player.ApplyInput(input);

                input.Dispose();
                Interlocked.Increment(ref _inputsPerSecond);
            }

            if (state.Inputs.IsExpired(now, InputTimeoutTicks))
            {
                state.Player.Stop();
            }

            state.Player.Update(_map);
            _ = _hubContext.Clients.All.SendAsync("updatePlayerState", state.Player);
        }
    }
    class ServerStats
    {
        public int Updates { get; set; }
        public int ProcessedInputs { get; set; }
    }

    /// <summary>
    /// This data structure tracks the list of active players. It optimizes for enumerating over all players quickly
    /// assuming the list of players rarely changes.
    /// </summary>
    class PlayerList
    {
        private readonly object _obj = new();

        private ImmutableArray<PlayerState> _playersStates = ImmutableArray<PlayerState>.Empty;
        private ImmutableArray<Player> _players = ImmutableArray<Player>.Empty;

        public ImmutableArray<Player> Players => _players;

        public ImmutableArray<PlayerState> PlayerStates => _playersStates;

        public void Add(PlayerState state)
        {
            lock (_obj)
            {
                _playersStates = _playersStates.Add(state);
                _players = _players.Add(state.Player);
            }
        }

        public bool TryRemove(string playerId, out PlayerState state)
        {
            lock (_obj)
            {
                var current = _playersStates;

                foreach (var item in current)
                {
                    if (item.PlayerId == playerId)
                    {
                        _playersStates = _playersStates.Remove(item);
                        _players = _players.Remove(item.Player);

                        state = item;

                        return true;
                    }
                }

                state = null;
                return false;
            }
        }

        public bool TryGet(string playerId, out PlayerState state)
        {
            // We don't need to lock here since we have a snapshot of the array
            foreach (var item in _playersStates)
            {
                if (item.PlayerId == playerId)
                {
                    state = item;
                    return true;
                }
            }

            state = null;
            return false;
        }
    }
}
