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

    public const int MAX_QUEUED_INPUTS_PER_PLAYER = 30;
    private static readonly TimeSpan GameLoopShutdownTimeout = TimeSpan.FromSeconds(5);

    // How long a bomb sits before exploding, and how long an explosion tile stays lethal/visible.
    private const int BOMB_FUSE_TICKS = FPS * 3;
    private const int EXPLOSION_DURATION_TICKS = FPS;
    private const int ROUND_RESET_DELAY_TICKS = FPS * 3;
    private const int POWERUP_SPAWN_PERCENT = 40;
    private const int MIN_PLAYERS_TO_START = 2;

    private static readonly string _mapData = "222222222222222" +
                             "200000000000002" +
                             "202020202020202" +
                             "203000300030002" +
                             "202020202020202" +
                             "203000300030002" +
                             "202020202020202" +
                             "203000300030002" +
                             "202020202020202" +
                             "203000300030002" +
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
    private readonly object _lifecycleBroadcastLock = new();
    private readonly Random _random = new();
    private readonly Func<int, int> _randomNext;

    // Bombs/explosions/powerups are only ever mutated from the single game loop thread inside
    // Update(), so no additional synchronization is required for these collections.
    private readonly List<Bomb> _bombs = new();
    private readonly List<Explosion> _explosions = new();
    private readonly List<Powerup> _powerups = new();
    private int _nextBombId;
    private int _roundResetTicksRemaining;

    private int _updatesPerSecond;
    private int _inputsPerSecond;
    private int _droppedInputs;
    private int _tickOverruns;
    private int _maxObservedDeltaMs;
    private int _maxQueueDepth;
    private Task _lastLifecycleBroadcast = Task.CompletedTask;

    public GameState(
        IHubContext<GameServer> hubContext,
        IHostApplicationLifetime hostApplicationLifetime,
        ILogger<GameState> logger,
        Func<int, int> randomNext = null)
    {
        _hubContext = hubContext;
        _hostApplicationLifetime = hostApplicationLifetime;
        _logger = logger;
        _randomNext = randomNext ?? _random.Next;

        _gameLoopThread = new Thread(_ => RunGameLoop())
        {
            IsBackground = true,
            Name = "BombRMan.GameLoop"
        };

        _gameLoopThread.Start();

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
            _availablePlayers.Push(CreatePlayer(i));
        }

        Task.Run(ServerStatsTimer);
    }

    private Player CreatePlayer(int index)
    {
        var pos = _initialPositions[index];
        return new Player
        {
            Index = index,
            X = pos.X,
            Y = pos.Y,
            ExactX = pos.X * POWER,
            ExactY = pos.Y * POWER,
            Direction = Direction.SOUTH,
            GameState = this,
            MaxBombs = 1,
            ActiveBombs = 0,
            PowerLevel = 1,
            Speed = 1,
            IsAlive = true,
        };
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

    public RoundState RoundState { get; private set; } = RoundState.WaitingForPlayers;

    public IReadOnlyList<Bomb> Bombs => _bombs;

    public IReadOnlyList<Explosion> Explosions => _explosions;

    public IReadOnlyList<Powerup> Powerups => _powerups;

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
            _availablePlayers.Push(CreatePlayer(state.Player.Index));

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

    internal static int TrimQueue(ConcurrentQueue<KeyboardState> queue, int maxSize)
    {
        var droppedCount = 0;

        while (queue.Count > maxSize && queue.TryDequeue(out var dropped))
        {
            dropped.Dispose();
            droppedCount++;
        }

        return droppedCount;
    }

    /// <summary>
    /// Given the elapsed time since the last catch-up pass and the fixed frame duration, returns
    /// how many simulation frames should run and whether the loop is falling behind schedule.
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
                    try
                    {
                        Update();
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Unhandled exception during game loop tick; continuing.");
                    }
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
                UpdatePlayer(state, input);

                input.Dispose();
                Interlocked.Increment(ref _inputsPerSecond);
            }

            if (state.Inputs.IsExpired(now, InputTimeoutTicks))
            {
                state.Player.Stop();
            }

            state.Player.Update(_map, IsBlockedByBomb);
            _ = _hubContext.Clients.All.SendAsync("updatePlayerState", state.Player);
        }

        UpdateBombs();
        UpdateExplosions();
        CheckPowerupPickups();
        CheckRoundLifecycle();
    }

    private void UpdatePlayer(PlayerState state, in KeyboardState input)
    {
        var player = state.Player;

        player.ApplyInput(input);

        var bombKeyDown = input[Keys.A];

        if (bombKeyDown && !state.PrevBombKeyDown)
        {
            TryPlaceBomb(player);
        }

        state.PrevBombKeyDown = bombKeyDown;
    }

    private bool IsBlockedByBomb(int x, int y)
    {
        foreach (var bomb in _bombs)
        {
            if (bomb.X == x && bomb.Y == y)
            {
                return true;
            }
        }

        return false;
    }

    private void TryPlaceBomb(Player player)
    {
        if (!BombLogic.CanPlaceBomb(player, _map, player.X, player.Y, _bombs))
        {
            return;
        }

        var bomb = new Bomb
        {
            Id = ++_nextBombId,
            OwnerIndex = player.Index,
            X = player.X,
            Y = player.Y,
            Power = player.PowerLevel,
            TicksRemaining = BOMB_FUSE_TICKS,
        };

        _bombs.Add(bomb);
        player.ActiveBombs++;

        _ = _hubContext.Clients.All.SendAsync("bombPlaced", bomb);
    }

    private void UpdateBombs()
    {
        List<Bomb> triggers = null;

        foreach (var bomb in _bombs)
        {
            if (bomb.Tick())
            {
                (triggers ??= new List<Bomb>()).Add(bomb);
            }
        }

        if (triggers is null)
        {
            return;
        }

        // Resolve the whole chain up front, then remove and explode. This keeps _bombs mutation
        // in one place and avoids the stale-index removal that recursion previously caused.
        var detonating = BombLogic.ResolveChainReaction(_bombs, triggers, _map);

        foreach (var bomb in detonating)
        {
            _bombs.Remove(bomb);
        }

        foreach (var bomb in detonating)
        {
            ExplodeBomb(bomb);
        }
    }

    private void ExplodeBomb(Bomb bomb)
    {
        foreach (var owner in ActivePlayers)
        {
            if (owner.Index == bomb.OwnerIndex)
            {
                owner.ActiveBombs = Math.Max(0, owner.ActiveBombs - 1);
                break;
            }
        }

        var blastTiles = ExplosionCalculator.GetBlastTiles(_map, bomb.X, bomb.Y, bomb.Power);

        foreach (var tile in blastTiles)
        {
            _explosions.Add(new Explosion
            {
                X = tile.X,
                Y = tile.Y,
                TicksRemaining = EXPLOSION_DURATION_TICKS,
            });

            if (_map[tile.X, tile.Y] == Tile.BRICK)
            {
                _map[tile.X, tile.Y] = Tile.GRASS;

                _ = _hubContext.Clients.All.SendAsync("mapTileChanged", new MapTileChange { X = tile.X, Y = tile.Y, Tile = Tile.GRASS });

                if (BombLogic.ShouldSpawnPowerup(_randomNext(100), POWERUP_SPAWN_PERCENT))
                {
                    var powerup = new Powerup
                    {
                        X = tile.X,
                        Y = tile.Y,
                        Type = (PowerupType)_randomNext(3),
                    };

                    _powerups.Add(powerup);

                    _ = _hubContext.Clients.All.SendAsync("powerupSpawned", powerup);
                }
            }
        }

        _ = _hubContext.Clients.All.SendAsync("bombExploded", new BombExploded { BombId = bomb.Id, Tiles = blastTiles });
    }

    private void UpdateExplosions()
    {
        for (int i = _explosions.Count - 1; i >= 0; i--)
        {
            var explosion = _explosions[i];

            foreach (var player in ActivePlayers)
            {
                if (player.IsAlive && player.X == explosion.X && player.Y == explosion.Y)
                {
                    player.IsAlive = false;

                    QueueLifecycleBroadcast("playerEliminated", player);
                }
            }

            if (explosion.Tick())
            {
                _explosions.RemoveAt(i);
            }
        }
    }

    private void CheckPowerupPickups()
    {
        for (int i = _powerups.Count - 1; i >= 0; i--)
        {
            var powerup = _powerups[i];

            foreach (var player in ActivePlayers)
            {
                if (player.IsAlive && player.X == powerup.X && player.Y == powerup.Y)
                {
                    BombLogic.ApplyPowerup(player, powerup.Type);
                    _powerups.RemoveAt(i);

                    _ = _hubContext.Clients.All.SendAsync("powerupCollected", new PowerupCollected { PlayerIndex = player.Index, Type = powerup.Type, X = powerup.X, Y = powerup.Y });
                    break;
                }
            }
        }
    }

    private void CheckRoundLifecycle()
    {
        var players = ActivePlayers;

        switch (RoundState)
        {
            case RoundState.WaitingForPlayers:
                if (players.Length >= MIN_PLAYERS_TO_START)
                {
                    RoundState = RoundState.InProgress;
                    QueueLifecycleBroadcast("roundStarted");
                }
                break;

            case RoundState.InProgress:
                if (BombLogic.IsRoundOver(players, out var winner))
                {
                    RoundState = RoundState.RoundOver;
                    _roundResetTicksRemaining = ROUND_RESET_DELAY_TICKS;

                    QueueLifecycleBroadcast("roundOver", new RoundOver { WinnerIndex = winner?.Index });
                }
                break;

            case RoundState.RoundOver:
                if (--_roundResetTicksRemaining <= 0)
                {
                    ResetRound();
                }
                break;
        }
    }

    private void ResetRound()
    {
        // Intentional sync-over-async: ResetRound only ever runs on the dedicated,
        // non-thread-pool game-loop thread (see RunGameLoop), which has no
        // SynchronizationContext to deadlock on. Blocking here guarantees any
        // in-flight roundOver/playerEliminated broadcast fully lands before we
        // mutate state and queue roundReset, preventing clients from observing a
        // reset out of order with the prior lifecycle event (see the reset-race
        // fix in QueueLifecycleBroadcast/SendLifecycleBroadcastAsync). A slow
        // client send can stall a tick, but that's already surfaced via
        // _tickOverruns rather than silently hidden.
        _lastLifecycleBroadcast.GetAwaiter().GetResult();

        _map.Reset();
        _bombs.Clear();
        _explosions.Clear();
        _powerups.Clear();

        foreach (var player in ActivePlayers)
        {
            var pos = _initialPositions[player.Index];
            player.X = pos.X;
            player.Y = pos.Y;
            player.ExactX = pos.X * POWER;
            player.ExactY = pos.Y * POWER;
            player.Direction = Direction.SOUTH;
            player.DirectionX = 0;
            player.DirectionY = 0;
            player.MaxBombs = 1;
            player.ActiveBombs = 0;
            player.PowerLevel = 1;
            player.Speed = 1;
            player.IsAlive = true;
        }

        RoundState = ActivePlayers.Length >= MIN_PLAYERS_TO_START ? RoundState.InProgress : RoundState.WaitingForPlayers;

        QueueLifecycleBroadcast("roundReset", new RoundReset
        {
            Map = _map.RawData,
            Players = ActivePlayers,
            RoundState = RoundState.ToString()
        });
    }

    private void QueueLifecycleBroadcast(string method, object argument = null)
    {
        lock (_lifecycleBroadcastLock)
        {
            _lastLifecycleBroadcast = SendLifecycleBroadcastAsync(_lastLifecycleBroadcast, method, argument);
        }
    }

    private async Task SendLifecycleBroadcastAsync(Task previous, string method, object argument)
    {
        await previous;

        if (argument is null)
        {
            await _hubContext.Clients.All.SendAsync(method);
        }
        else
        {
            await _hubContext.Clients.All.SendAsync(method, argument);
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

    class MapTileChange
    {
        public int X { get; set; }
        public int Y { get; set; }
        public Tile Tile { get; set; }
    }

    class BombExploded
    {
        public int BombId { get; set; }
        public List<Point> Tiles { get; set; }
    }

    class PowerupCollected
    {
        public int PlayerIndex { get; set; }
        public PowerupType Type { get; set; }
        public int X { get; set; }
        public int Y { get; set; }
    }

    class RoundOver
    {
        public int? WinnerIndex { get; set; }
    }

    class RoundReset
    {
        public string Map { get; set; }
        public ImmutableArray<Player> Players { get; set; }
        public string RoundState { get; set; }
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
