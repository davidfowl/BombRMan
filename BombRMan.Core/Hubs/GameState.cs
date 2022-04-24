using System.Buffers;
using System.Collections.Concurrent;
using System.Drawing;
using System.Runtime.InteropServices;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.ObjectPool;

namespace BombRMan.Hubs;

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
    private readonly ConcurrentStack<Player> _availablePlayers = new();
    private readonly ConcurrentDictionary<string, PlayerState> _activePlayers = new();
    private readonly ObjectPool<KeyboardState> _pool;
    private readonly Map _map = new(_mapData, 15, 13, 32);
    private readonly IHubContext<GameServer> _hubContext;
    private readonly IHostApplicationLifetime _hostApplicationLifetime;
    private int _updatesPerSecond;
    private int _inputsPerSecond;

    public GameState(IHubContext<GameServer> hubContext, ObjectPool<KeyboardState> pool, IHostApplicationLifetime hostApplicationLifetime)
    {
        _hubContext = hubContext;
        _hostApplicationLifetime = hostApplicationLifetime;
        _pool = pool;

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

        while (await timer.WaitForNextTickAsync())
        {
            var updates = Interlocked.Exchange(ref _updatesPerSecond, 0);
            var inputs = Interlocked.Exchange(ref _inputsPerSecond, 0);
            await _hubContext.Clients.All.SendAsync("serverStats", new { Updates = updates, ProcessedInputs = inputs });
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
        if (_activePlayers.TryGetValue(playerId, out var state))
        {
            foreach (var input in inputs)
            {
                if (input is null) break;

                state.Inputs.Enqueue(input);
            }

            // Return the batch to the pool, clear the array so we can use null to figure out what
            // the last entry is without storing a struct on the heap
            ArrayPool<KeyboardState>.Shared.Return(inputs, clearArray: true);
        }
    }

    public void RunGameLoop()
    {
        var frameTicks = (int)Math.Round(1000.0 / FPS);
        var lastUpdate = Environment.TickCount;

        while (!_hostApplicationLifetime.ApplicationStopping.IsCancellationRequested)
        {
            int update = Environment.TickCount;
            // Get difference
            int delta = update - lastUpdate;
            // Loop while difference is greater than a frame tick
            while (delta > frameTicks)
            {
                delta -= frameTicks;

                Update();
            }

            // Remove the carry over delta from update and store as lastUpdate
            lastUpdate = update - delta;

            Thread.Sleep(1);
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
                _pool.Return(input);
                _ = _hubContext.Clients.All.SendAsync("updatePlayerState", pair.Value.Player);
            }
            Interlocked.Increment(ref _inputsPerSecond);
        }
    }
}
