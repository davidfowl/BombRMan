using System.Buffers;
using System.Collections.Immutable;
using System.Collections.Concurrent;
using System.Drawing;
using System.Diagnostics;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;

namespace BombRMan.Hubs;

public class GameState
{
    public const int POWER = 100;
    public const int DELTA = 10;
    public const int FPS = 60;
    private static readonly long InputTimeoutTicks = Stopwatch.Frequency / 4;

    // Bounds how many un-processed inputs we'll hold per player before dropping the oldest ones.
    // Prevents a slow-draining/disconnected client from growing its queue unbounded.
    public const int MAX_QUEUED_INPUTS_PER_PLAYER = 30;

    // How long we wait for the game loop thread to observe cancellation and exit before logging a warning.
    private static readonly TimeSpan GameLoopShutdownTimeout = TimeSpan.FromSeconds(5);

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
    private readonly ILogger<GameState> _logger;
    private readonly Thread _gameLoopThread;
    private readonly ManualResetEventSlim _gameLoopStopped = new(initialState: false);
    private int _updatesPerSecond;
    private int _inputsPerSecond;
    private int _droppedInputs;
    private int _tickOverruns;
    private int _maxObservedDeltaMs;
    private int _maxQueueDepth;

    public GameState(IHubContext<GameServer> hubContext, IHostApplicationLifetime hostApplicationLifetime, ILogger<GameState> logger)
    {
        _hubContext = hubContext;
        _hostApplicationLifetime = hostApplicationLifetime;
        _logger = logger;

        _gameLoopThread = new Thread(_ => RunGameLoop())
        {
            IsBackground = true,
            Name = "BombRMan.GameLoop"
        };

        _gameLoopThread.Start();

        // Best-effort: log (and wait for) an orderly game loop shutdown when the host starts stopping.
        // The thread is a background thread so it will not block process exit on its own, but we want
        // visibility into whether the loop actually observed the cancellation in a timely fashion.
        _hostApplicationLifetime.ApplicationStopping.Register(() =>
        {
            _logger.LogInformation("Game loop shutdown requested.");

            if (_gameLoopStopped.Wait(GameLoopShutdownTimeout))
            {
                _logger.LogInformation("Game loop stopped cleanly.");
            }
            else
            {
                _logger.LogWarning("Game loop did not stop within {TimeoutSeconds}s of shutdown being requested.", GameLoopShutdownTimeout.TotalSeconds);
            }
        });

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
            var dropped = Interlocked.Exchange(ref _droppedInputs, 0);
            var overruns = Interlocked.Exchange(ref _tickOverruns, 0);
            var maxDelta = Interlocked.Exchange(ref _maxObservedDeltaMs, 0);
            var maxQueueDepth = Interlocked.Exchange(ref _maxQueueDepth, 0);
            var activePlayers = _activePlayers.PlayerStates;

            var queueDepth = 0;
            foreach (var state in activePlayers)
            {
                queueDepth += state.Inputs.Count;
            }

            serverStats.Updates = updates;
            serverStats.ProcessedInputs = inputs;
            serverStats.DroppedInputs = dropped;
            serverStats.TickOverruns = overruns;
            serverStats.MaxTickDeltaMs = maxDelta;
            serverStats.QueueDepth = queueDepth;
            serverStats.MaxQueueDepth = maxQueueDepth;
            serverStats.ActivePlayers = activePlayers.Length;
            serverStats.AvailablePlayerSlots = _availablePlayers.Count;

            if (dropped > 0)
            {
                _logger.LogWarning("Dropped {DroppedInputs} input(s) due to queue overflow in the last interval.", dropped);
            }

            if (overruns > 0)
            {
                _logger.LogWarning("Game loop fell behind schedule: {TickOverruns} overrun frame(s), max observed delta {MaxTickDeltaMs}ms.", overruns, maxDelta);
            }

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

    private static void InterlockedMax(ref int location, int value)
    {
        int initial, computed;
        do
        {
            initial = Volatile.Read(ref location);
            computed = Math.Max(initial, value);
        }
        while (Interlocked.CompareExchange(ref location, computed, initial) != initial);
    }

    /// <summary>
    /// Given the elapsed time since the last catch-up pass (<paramref name="delta"/>) and the
    /// fixed frame duration (<paramref name="frameTicks"/>), returns how many simulation frames
    /// should run this pass, the leftover carry-over delta, and whether the loop is falling
    /// behind schedule (more than one catch-up iteration was needed).
    /// </summary>
    internal static (int Iterations, int RemainingDelta, bool IsOverrun) ComputeFrameAdvance(int delta, int frameTicks)
    {
        var iterations = 0;

        while (delta >= frameTicks)
        {
            delta -= frameTicks;
            iterations++;
        }

        return (iterations, delta, iterations > 1);
    }

    public void RunGameLoop()
    {
        long lastUpdate = Stopwatch.GetTimestamp();
        long accumulatedTicks = 0;

        try
        {
            while (!_hostApplicationLifetime.ApplicationStopping.IsCancellationRequested)
            {
                long update = Stopwatch.GetTimestamp();
                var elapsed = update - lastUpdate;
                accumulatedTicks += elapsed * FPS;
                lastUpdate = update;

                var deltaMs = (int)Math.Min(int.MaxValue, elapsed * 1000 / Stopwatch.Frequency);
                InterlockedMax(ref _maxObservedDeltaMs, deltaMs);

                var iterations = 0;
                while (accumulatedTicks >= Stopwatch.Frequency)
                {
                    accumulatedTicks -= Stopwatch.Frequency;
                    Update();
                    iterations++;
                }

                if (iterations > 1)
                {
                    Interlocked.Increment(ref _tickOverruns);
                }

                Thread.Sleep(1);
            }
        }
        finally
        {
            _gameLoopStopped.Set();
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
        public int DroppedInputs { get; set; }
        public int TickOverruns { get; set; }
        public int MaxTickDeltaMs { get; set; }
        public int QueueDepth { get; set; }
        public int MaxQueueDepth { get; set; }
        public int ActivePlayers { get; set; }
        public int AvailablePlayerSlots { get; set; }
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
