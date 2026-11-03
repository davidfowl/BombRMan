using System.Threading.Channels;
using BombRMan.Hubs;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace BombRMan.Core.Tests;

public class GameServerIntegrationTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public GameServerIntegrationTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task LatestInputStateDrivesAuthoritativeMovement()
    {
        await using var connection = CreateConnection();
        var updates = Channel.CreateUnbounded<PlayerSnapshot>();
        var initialized = new TaskCompletionSource<PlayerSnapshot>(TaskCreationOptions.RunContinuationsAsynchronously);

        connection.On<PlayerSnapshot>("initializePlayer", player => initialized.TrySetResult(player));
        connection.On<PlayerSnapshot>("updatePlayerState", update => updates.Writer.TryWrite(update));

        await connection.StartAsync();
        var initial = await initialized.Task.WaitAsync(TimeSpan.FromSeconds(5));

        await connection.SendAsync("SendKeys", new[]
        {
            CreateInput(1, Keys.LEFT),
            CreateInput(2, Keys.RIGHT)
        });

        var moving = await WaitForQueueAsync(
            updates,
            player => player.LastProcessed == 2 && player.ExactX > initial.ExactX,
            TimeSpan.FromSeconds(5));

        Assert.Equal(1, moving.DirectionX);

        await connection.SendAsync("SendKeys", new[] { CreateInput(3) });

        var stopped = await WaitForQueueAsync(
            updates,
            player => player.LastProcessed == 3 && player.DirectionX == 0 && player.DirectionY == 0,
            TimeSpan.FromSeconds(5));

        Assert.Equal(moving.ExactX, stopped.ExactX);
    }

    [Fact]
    public async Task ClientJoiningMidRoundReceivesCurrentRoundState()
    {
        await using var factory = _factory.WithWebHostBuilder(_ => { });
        await using var first = CreateConnection(factory);
        await using var second = CreateConnection(factory);
        await using var joining = CreateConnection(factory);
        var initialRoundStarted = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var joiningRoundStarted = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);

        first.On("roundStarted", () => initialRoundStarted.TrySetResult());
        second.On("roundStarted", () => initialRoundStarted.TrySetResult());
        joining.On("roundStarted", () => joiningRoundStarted.TrySetResult());

        await first.StartAsync();
        await second.StartAsync();
        await initialRoundStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));

        await joining.StartAsync();

        await joiningRoundStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task RoundResetBroadcastReplacesClientStateAtomically()
    {
        await using var first = CreateConnection();
        await using var second = CreateConnection();
        var reset = new TaskCompletionSource<RoundResetSnapshot>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var roundOver = new TaskCompletionSource<RoundOverSnapshot>(
            TaskCreationOptions.RunContinuationsAsynchronously);

        first.On<RoundResetSnapshot>("roundReset", value => reset.TrySetResult(value));
        first.On<RoundOverSnapshot>("roundOver", value => roundOver.TrySetResult(value));
        second.On<RoundOverSnapshot>("roundOver", value => roundOver.TrySetResult(value));

        await first.StartAsync();
        await second.StartAsync();
        await Task.Delay(100);

        await first.SendAsync("SendKeys", new[] { CreateInput(1, Keys.A) });

        var snapshot = await reset.Task.WaitAsync(TimeSpan.FromSeconds(15));
        await roundOver.Task.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.Equal("InProgress", snapshot.RoundState);
        Assert.Equal("222222222222222", snapshot.Map[..15]);
        Assert.Equal(2, snapshot.Players.Count);
        Assert.All(snapshot.Players, player => Assert.False(player.Eliminated));
        Assert.Contains(snapshot.Players, player => player.X == 1 && player.Y == 1);
        Assert.Contains(snapshot.Players, player => player.X == 13 && player.Y == 1);
    }

    [Fact]
    public async Task DefaultMapHasBricksAndClearSpawnExits()
    {
        await using var connection = CreateConnection();
        var mapTask = new TaskCompletionSource<string>(
            TaskCreationOptions.RunContinuationsAsynchronously);

        connection.On<string>("initializeMap", map => mapTask.TrySetResult(map));
        await connection.StartAsync();

        var map = await mapTask.Task.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.Contains('3', map);
        Assert.Equal('0', map[1 * 15 + 1]);
        Assert.Equal('0', map[1 * 15 + 2]);
        Assert.Equal('0', map[1 * 15 + 12]);
        Assert.Equal('0', map[1 * 15 + 13]);
        Assert.Equal('0', map[11 * 15 + 1]);
        Assert.Equal('0', map[11 * 15 + 2]);
        Assert.Equal('0', map[11 * 15 + 12]);
        Assert.Equal('0', map[11 * 15 + 13]);
    }

    [Fact]
    public async Task DeterministicPowerupSpawnAndCollectionSynchronizesAcrossClients()
    {
        await using var factory = _factory.WithWebHostBuilder(builder =>
            builder.ConfigureTestServices(services =>
                services.AddSingleton<Func<int, int>>(_ => _ => 0)));
        await using var first = CreateConnection(factory);
        await using var second = CreateConnection(factory);
        var spawned = Channel.CreateUnbounded<PowerupSnapshot>();
        var collected = Channel.CreateUnbounded<PowerupCollectedSnapshot>();
        var tileChanges = Channel.CreateUnbounded<MapTileChangeSnapshot>();
        var updates = Channel.CreateUnbounded<PlayerSnapshot>();
        var firstInitialized = new TaskCompletionSource<PlayerSnapshot>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var secondInitialized = new TaskCompletionSource<PlayerSnapshot>(
            TaskCreationOptions.RunContinuationsAsynchronously);

        first.On<PowerupSnapshot>("powerupSpawned", value => spawned.Writer.TryWrite(value));
        second.On<PowerupSnapshot>("powerupSpawned", value => spawned.Writer.TryWrite(value));
        first.On<PowerupCollectedSnapshot>("powerupCollected", value => collected.Writer.TryWrite(value));
        second.On<PowerupCollectedSnapshot>("powerupCollected", value => collected.Writer.TryWrite(value));
        first.On<MapTileChangeSnapshot>("mapTileChanged", value => tileChanges.Writer.TryWrite(value));
        second.On<MapTileChangeSnapshot>("mapTileChanged", value => tileChanges.Writer.TryWrite(value));
        first.On<PlayerSnapshot>("updatePlayerState", value => updates.Writer.TryWrite(value));
        second.On<PlayerSnapshot>("updatePlayerState", value => updates.Writer.TryWrite(value));
        first.On<PlayerSnapshot>("initializePlayer", player => firstInitialized.TrySetResult(player));
        second.On<PlayerSnapshot>("initializePlayer", player => secondInitialized.TrySetResult(player));

        await first.StartAsync();
        await second.StartAsync();
        await Task.WhenAll(
            firstInitialized.Task.WaitAsync(TimeSpan.FromSeconds(20)),
            secondInitialized.Task.WaitAsync(TimeSpan.FromSeconds(20)));

        var firstPlayer = await firstInitialized.Task;
        var secondPlayer = await secondInitialized.Task;
        var initial = firstPlayer.X == 1 ? firstPlayer : secondPlayer;
        var driver = firstPlayer.X == 1 ? first : second;
        var driverIndex = initial.Index;
        var targetX = initial.X == 1 ? 2 : 10;
        var horizontalKey = initial.X == 1 ? Keys.RIGHT : Keys.LEFT;
        var gameState = factory.Services.GetRequiredService<GameState>();
        var inputId = 0;
        inputId = await SendInputLoopAsync(driver, inputId, Keys.DOWN, 4);
        if (initial.X != 1)
        {
            inputId = await SendInputLoopAsync(driver, inputId, Keys.LEFT, 4);
        }
        inputId = await SendInputLoopAsync(driver, inputId, null, 2);
        inputId = await SendInputLoopAsync(driver, inputId, Keys.A, 2);

        // The server only retains the single most-recently received input per player
        // (see PlayerInputBuffer), so if the next movement input is sent before the game
        // loop has actually consumed the bomb-key input, the edge-triggered bomb placement
        // can be silently coalesced away and lost - most likely to happen when the game
        // loop thread is starved under heavy system load (e.g. a full parallel test run).
        // Poll for the server-authoritative bomb to actually exist before moving the driver
        // away, instead of assuming a fixed number of wall-clock-paced sends was enough.
        await WaitForConditionAsync(
            () => gameState.Bombs.Any(bomb => bomb.OwnerIndex == driverIndex),
            TimeSpan.FromSeconds(20));

        inputId = await SendInputLoopAsync(driver, inputId, Keys.DOWN, 20);
        inputId = await SendInputLoopAsync(driver, inputId, null, 2);

        var powerup = await WaitForQueueAsync(
            spawned,
            value => value.X == targetX && value.Y == 3,
            TimeSpan.FromSeconds(20));
        var secondPowerup = await WaitForQueueAsync(
            spawned,
            value => value.X == powerup.X && value.Y == powerup.Y && value.Type == powerup.Type,
            TimeSpan.FromSeconds(20));
        var firstTileChange = await WaitForQueueAsync(
            tileChanges,
            value => value.X == targetX && value.Y == 3 && value.Tile == Tile.GRASS,
            TimeSpan.FromSeconds(20));
        var secondTileChange = await WaitForQueueAsync(
            tileChanges,
            value => value.X == targetX && value.Y == 3 && value.Tile == Tile.GRASS,
            TimeSpan.FromSeconds(20));
        Assert.Equal(powerup.Type, secondPowerup.Type);
        Assert.Equal(Tile.GRASS, firstTileChange.Tile);
        Assert.Equal(Tile.GRASS, secondTileChange.Tile);

        inputId = await SendInputLoopAsync(driver, inputId, Keys.UP, 13);
        inputId = await SendInputLoopAsync(driver, inputId, horizontalKey, 3);
        inputId = await SendInputLoopAsync(driver, inputId, null, 3);
        Player driverState;

        // Nudge the driver onto the powerup's tile one correction at a time. Under heavy
        // system load (e.g. the full suite running in parallel) wall-clock delays can stretch
        // well beyond their nominal duration, so how far a single correction input moves the
        // player is not predictable from elapsed real time alone. Rather than bounding this by
        // a fixed number of attempts (which assumes each attempt advances the player a roughly
        // known distance within a roughly known time), poll against a generous wall-clock
        // deadline and keep correcting until the server-authoritative position actually matches,
        // so the test converges regardless of how much real time each step actually took.
        var correctionDeadline = DateTime.UtcNow + TimeSpan.FromSeconds(30);
        while (true)
        {
            await Task.Delay(50);
            driverState = gameState.ActivePlayers.Single(player => player.Index == driverIndex);
            if (driverState.X == powerup.X && driverState.Y == powerup.Y)
            {
                break;
            }

            if (DateTime.UtcNow >= correctionDeadline)
            {
                break;
            }

            var correction = driverState.X < powerup.X ? Keys.RIGHT :
                driverState.X > powerup.X ? Keys.LEFT :
                driverState.Y < powerup.Y ? Keys.DOWN : Keys.UP;
            inputId = await SendInputLoopAsync(driver, inputId, correction, 1);
        }

        driverState = gameState.ActivePlayers.Single(player => player.Index == driverIndex);
        Assert.True(driverState.IsAlive);
        Assert.Equal(powerup.X, driverState.X);
        Assert.Equal(powerup.Y, driverState.Y);

        var collection = await WaitForQueueAsync(
            collected,
            value => value.X == powerup.X && value.Y == powerup.Y,
            TimeSpan.FromSeconds(20));
        var secondCollection = await WaitForQueueAsync(
            collected,
            value => value.X == collection.X &&
                value.Y == collection.Y &&
                value.PlayerIndex == collection.PlayerIndex &&
                value.Type == collection.Type,
            TimeSpan.FromSeconds(20));

        Assert.Equal(collection.PlayerIndex, secondCollection.PlayerIndex);
        Assert.Equal(powerup.Type, collection.Type);
        Assert.Equal(powerup.X, collection.X);
        Assert.Equal(powerup.Y, collection.Y);

        var collectorUpdate = await WaitForQueueAsync(
            updates,
            value => value.Index == collection.PlayerIndex &&
                value.Speed == 2 &&
                value.LastProcessed > 0,
            TimeSpan.FromSeconds(20));

        Assert.Equal(2, collectorUpdate.Speed);
        var collector = gameState.ActivePlayers.Single(player => player.Index == collection.PlayerIndex);
        var otherPlayer = gameState.ActivePlayers.Single(player => player.Index != collection.PlayerIndex);
        Assert.Equal(2, collector.Speed);
        Assert.Equal(1, otherPlayer.Speed);
        Assert.Empty(gameState.Powerups);

        inputId = await SendInputLoopAsync(driver, inputId, horizontalKey, 10);
        var moved = await WaitForQueueAsync(
            updates,
            value => value.Index == collection.PlayerIndex &&
                (value.X != powerup.X || value.Y != powerup.Y),
            TimeSpan.FromSeconds(20));
        Assert.True(moved.X != powerup.X || moved.Y != powerup.Y);
    }

    private HubConnection CreateConnection() =>
        CreateConnection(_factory);

    private static HubConnection CreateConnection(WebApplicationFactory<Program> factory) =>
        new HubConnectionBuilder()
            .WithUrl(new Uri(factory.Server.BaseAddress, "game"), options =>
            {
                options.HttpMessageHandlerFactory = _ => factory.Server.CreateHandler();
                options.Transports = HttpTransportType.LongPolling;
            })
            .Build();

    private static async Task<int> SendInputLoopAsync(
        HubConnection connection,
        int inputId,
        Keys? key,
        int count)
    {
        for (var i = 0; i < count; i++)
        {
            await connection.SendAsync(
                "SendKeys",
                new[] { CreateInput(++inputId, key) });
            await Task.Delay(100);
        }

        return inputId;
    }

    private static KeyboardState CreateInput(int id, params Keys?[] keys)
    {
        var keyState = new uint[8];
        foreach (var key in keys)
        {
            if (key is null)
            {
                continue;
            }

            keyState[(int)key.Value >> 5] |= 1u << ((int)key.Value & 0x1f);
        }

        return new KeyboardState(keyState, id, 0);
    }

    /// <summary>
    /// Polls server-authoritative state until <paramref name="condition"/> is true, backing off
    /// slightly between checks. Used instead of a fixed wall-clock delay so the test converges
    /// based on actual observed state rather than an assumed elapsed-time budget.
    /// </summary>
    private static async Task WaitForConditionAsync(Func<bool> condition, TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        var delay = TimeSpan.FromMilliseconds(25);

        while (!condition())
        {
            if (DateTime.UtcNow >= deadline)
            {
                throw new TimeoutException("The expected server-side condition was not observed.");
            }

            await Task.Delay(delay);
            delay = TimeSpan.FromMilliseconds(Math.Min(delay.TotalMilliseconds * 2, 250));
        }
    }

    private static async Task<T> WaitForQueueAsync<T>(
        Channel<T> channel,
        Func<T, bool> predicate,
        TimeSpan? timeout = null)
    {
        using var cts = new CancellationTokenSource(timeout ?? TimeSpan.FromSeconds(10));

        try
        {
            await foreach (var value in channel.Reader.ReadAllAsync(cts.Token))
            {
                if (predicate(value))
                {
                    return value;
                }
            }

            throw new TimeoutException("The expected SignalR event was not received.");
        }
        catch (OperationCanceledException) when (cts.IsCancellationRequested)
        {
            throw new TimeoutException("The expected SignalR event was not received.");
        }
    }

    private sealed class PlayerSnapshot
    {
        public int Index { get; init; }
        public int ExactX { get; init; }
        public int ExactY { get; init; }
        public int X { get; init; }
        public int Y { get; init; }
        public int Speed { get; init; }
        public int DirectionX { get; init; }
        public int DirectionY { get; init; }
        public int LastProcessed { get; init; }
    }

    private class PowerupSnapshot
    {
        public int X { get; init; }
        public int Y { get; init; }
        public PowerupType Type { get; init; }
    }

    private sealed class PowerupCollectedSnapshot : PowerupSnapshot
    {
        public int PlayerIndex { get; init; }
    }

    private sealed class MapTileChangeSnapshot
    {
        public int X { get; init; }
        public int Y { get; init; }
        public Tile Tile { get; init; }
    }

    private sealed class RoundResetSnapshot
    {
        public string Map { get; init; } = string.Empty;
        public string RoundState { get; init; } = string.Empty;
        public List<RoundResetPlayer> Players { get; init; } = new();
    }

    private sealed class RoundOverSnapshot
    {
        public int? WinnerIndex { get; init; }
    }

    private sealed class RoundResetPlayer
    {
        public int X { get; init; }
        public int Y { get; init; }
        public bool Eliminated { get; init; }
    }
}
