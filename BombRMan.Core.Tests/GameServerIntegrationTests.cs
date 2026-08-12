using System.Collections.Concurrent;
using BombRMan.Hubs;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.SignalR.Client;
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

        var moving = await WaitForUpdateAsync(
            updates,
            player => player.LastProcessed == 2 && player.ExactX > initial.ExactX);

        Assert.Equal(1, moving.DirectionX);

        await connection.SendAsync("SendKeys", new[] { CreateInput(3) });

        var stopped = await WaitForUpdateAsync(
            updates,
            player => player.LastProcessed == 3 && player.DirectionX == 0 && player.DirectionY == 0);

        Assert.Equal(moving.ExactX, stopped.ExactX);
    }

    [Fact]
    public async Task RoundResetBroadcastReplacesClientStateAtomically()
    {
        await using var first = CreateConnection();
        await using var second = CreateConnection();
        var reset = new TaskCompletionSource<RoundResetSnapshot>(
            TaskCreationOptions.RunContinuationsAsynchronously);

        first.On<RoundResetSnapshot>("roundReset", value => reset.TrySetResult(value));

        await first.StartAsync();
        await second.StartAsync();
        await Task.Delay(100);

        await first.SendAsync("SendKeys", new[] { CreateInput(1, Keys.A) });

        var snapshot = await reset.Task.WaitAsync(TimeSpan.FromSeconds(15));

        Assert.Equal("InProgress", snapshot.RoundState);
        Assert.Equal("222222222222222", snapshot.Map[..15]);
        Assert.Equal(2, snapshot.Players.Count);
        Assert.All(snapshot.Players, player => Assert.False(player.Eliminated));
        Assert.Contains(snapshot.Players, player => player.X == 1 && player.Y == 1);
        Assert.Contains(snapshot.Players, player => player.X == 13 && player.Y == 1);
    }

    private HubConnection CreateConnection() =>
        new HubConnectionBuilder()
            .WithUrl(new Uri(_factory.Server.BaseAddress, "game"), options =>
            {
                options.HttpMessageHandlerFactory = _ => _factory.Server.CreateHandler();
                options.Transports = HttpTransportType.LongPolling;
            })
            .Build();

    private static KeyboardState CreateInput(int id, params Keys[] keys)
    {
        var keyState = new uint[8];
        foreach (var key in keys)
        {
            keyState[(int)key >> 5] |= 1u << ((int)key & 0x1f);
        }

        return new KeyboardState(keyState, id, 0);
    }

    private static async Task<PlayerSnapshot> WaitForUpdateAsync(
        ConcurrentQueue<PlayerSnapshot> updates,
        Func<PlayerSnapshot, bool> predicate)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));

        while (!timeout.IsCancellationRequested)
        {
            while (updates.TryDequeue(out var update))
            {
                if (predicate(update))
                {
                    return update;
                }
            }

            await Task.Delay(10, timeout.Token);
        }

        throw new TimeoutException("The expected authoritative player update was not received.");
    }

    private sealed class PlayerSnapshot
    {
        public int ExactX { get; init; }
        public int ExactY { get; init; }
        public int DirectionX { get; init; }
        public int DirectionY { get; init; }
        public int LastProcessed { get; init; }
    }

    private sealed class RoundResetSnapshot
    {
        public string Map { get; init; } = string.Empty;
        public string RoundState { get; init; } = string.Empty;
        public List<RoundResetPlayer> Players { get; init; } = new();
    }

    private sealed class RoundResetPlayer
    {
        public int X { get; init; }
        public int Y { get; init; }
        public bool Eliminated { get; init; }
    }
}
