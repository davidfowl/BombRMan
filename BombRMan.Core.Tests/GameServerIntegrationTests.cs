using System.Collections.Concurrent;
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
        var updates = new ConcurrentQueue<PlayerSnapshot>();
        var initialized = new TaskCompletionSource<PlayerSnapshot>(TaskCreationOptions.RunContinuationsAsynchronously);

        connection.On<PlayerSnapshot>("initializePlayer", player => initialized.TrySetResult(player));
        connection.On<PlayerSnapshot>("updatePlayerState", updates.Enqueue);

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
        var spawned = new ConcurrentQueue<PowerupSnapshot>();
        var collected = new ConcurrentQueue<PowerupCollectedSnapshot>();
        var tileChanges = new ConcurrentQueue<MapTileChangeSnapshot>();
        var updates = new ConcurrentQueue<PlayerSnapshot>();
        var firstInitialized = new TaskCompletionSource<PlayerSnapshot>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var secondInitialized = new TaskCompletionSource<PlayerSnapshot>(
            TaskCreationOptions.RunContinuationsAsynchronously);

        first.On<PowerupSnapshot>("powerupSpawned", spawned.Enqueue);
        second.On<PowerupSnapshot>("powerupSpawned", spawned.Enqueue);
        first.On<PowerupCollectedSnapshot>("powerupCollected", collected.Enqueue);
        second.On<PowerupCollectedSnapshot>("powerupCollected", collected.Enqueue);
        first.On<MapTileChangeSnapshot>("mapTileChanged", tileChanges.Enqueue);
        second.On<MapTileChangeSnapshot>("mapTileChanged", tileChanges.Enqueue);
        first.On<PlayerSnapshot>("updatePlayerState", updates.Enqueue);
        second.On<PlayerSnapshot>("updatePlayerState", updates.Enqueue);
        first.On<PlayerSnapshot>("initializePlayer", player => firstInitialized.TrySetResult(player));
        second.On<PlayerSnapshot>("initializePlayer", player => secondInitialized.TrySetResult(player));

        await first.StartAsync();
        await second.StartAsync();
        await Task.WhenAll(
            firstInitialized.Task.WaitAsync(TimeSpan.FromSeconds(5)),
            secondInitialized.Task.WaitAsync(TimeSpan.FromSeconds(5)));

        var firstPlayer = await firstInitialized.Task;
        var secondPlayer = await secondInitialized.Task;
        var initial = firstPlayer.X == 1 ? firstPlayer : secondPlayer;
        var driver = firstPlayer.X == 1 ? first : second;
        var driverIndex = initial.Index;
        var targetX = initial.X == 1 ? 2 : 10;
        var horizontalKey = initial.X == 1 ? Keys.RIGHT : Keys.LEFT;
        var inputId = 0;
        inputId = await SendInputLoopAsync(driver, inputId, Keys.DOWN, 4);
        if (initial.X != 1)
        {
            inputId = await SendInputLoopAsync(driver, inputId, Keys.LEFT, 4);
        }
        inputId = await SendInputLoopAsync(driver, inputId, null, 2);
        inputId = await SendInputLoopAsync(driver, inputId, Keys.A, 2);
        inputId = await SendInputLoopAsync(driver, inputId, Keys.DOWN, 20);
        inputId = await SendInputLoopAsync(driver, inputId, null, 2);

        var powerup = await WaitForQueueAsync(
            spawned,
            value => value.X == targetX && value.Y == 3);
        var secondPowerup = await WaitForQueueAsync(
            spawned,
            value => value.X == powerup.X && value.Y == powerup.Y && value.Type == powerup.Type);
        var firstTileChange = await WaitForQueueAsync(
            tileChanges,
            value => value.X == targetX && value.Y == 3 && value.Tile == Tile.GRASS);
        var secondTileChange = await WaitForQueueAsync(
            tileChanges,
            value => value.X == targetX && value.Y == 3 && value.Tile == Tile.GRASS);
        Assert.Equal(powerup.Type, secondPowerup.Type);
        Assert.Equal(Tile.GRASS, firstTileChange.Tile);
        Assert.Equal(Tile.GRASS, secondTileChange.Tile);

        inputId = await SendInputLoopAsync(driver, inputId, Keys.UP, 13);
        inputId = await SendInputLoopAsync(driver, inputId, horizontalKey, 3);
        inputId = await SendInputLoopAsync(driver, inputId, null, 3);
        var gameState = factory.Services.GetRequiredService<GameState>();
        Player driverState;
        for (var attempt = 0; attempt < 10; attempt++)
        {
            await Task.Delay(100);
            driverState = gameState.ActivePlayers.Single(player => player.Index == driverIndex);
            if (driverState.X == powerup.X && driverState.Y == powerup.Y)
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
            value => value.X == powerup.X && value.Y == powerup.Y);
        var secondCollection = await WaitForQueueAsync(
            collected,
            value => value.X == collection.X &&
                value.Y == collection.Y &&
                value.PlayerIndex == collection.PlayerIndex &&
                value.Type == collection.Type);

        Assert.Equal(collection.PlayerIndex, secondCollection.PlayerIndex);
        Assert.Equal(powerup.Type, collection.Type);
        Assert.Equal(powerup.X, collection.X);
        Assert.Equal(powerup.Y, collection.Y);

        var collectorUpdate = await WaitForQueueAsync(
            updates,
            value => value.Index == collection.PlayerIndex &&
                value.Speed == 2 &&
                value.LastProcessed > 0);

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
                (value.X != powerup.X || value.Y != powerup.Y));
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

    private static async Task<T> WaitForQueueAsync<T>(
        ConcurrentQueue<T> queue,
        Func<T, bool> predicate,
        TimeSpan? timeout = null)
    {
        using var cts = new CancellationTokenSource(timeout ?? TimeSpan.FromSeconds(10));

        try
        {
            while (true)
            {
                while (queue.TryDequeue(out var value))
                {
                    if (predicate(value))
                    {
                        return value;
                    }
                }

                await Task.Delay(10, cts.Token);
            }
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
