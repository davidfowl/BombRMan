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
}
