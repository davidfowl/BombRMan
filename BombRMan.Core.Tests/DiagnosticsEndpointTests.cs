using System.Net.Http.Json;
using System.Threading.Channels;
using BombRMan.Hubs;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.SignalR.Client;
using Xunit;

namespace BombRMan.Core.Tests;

public class DiagnosticsEndpointTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public DiagnosticsEndpointTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task DiagnosticsEndpointReportsWaitingForPlayersWithNoConnections()
    {
        await using var factory = _factory.WithWebHostBuilder(_ => { });
        using var client = factory.CreateClient();

        var snapshot = await client.GetFromJsonAsync<DiagnosticsSnapshot>("/diagnostics");

        Assert.NotNull(snapshot);
        Assert.Equal("WaitingForPlayers", snapshot.RoundState);
        Assert.Empty(snapshot.Players);
        Assert.Empty(snapshot.Bombs);
        Assert.NotNull(snapshot.Map);
        Assert.Equal(15, snapshot.Map.Width);
        Assert.Equal(13, snapshot.Map.Height);
    }

    [Fact]
    public async Task DiagnosticsEndpointReflectsLiveInProgressRound()
    {
        await using var factory = _factory.WithWebHostBuilder(_ => { });
        using var client = factory.CreateClient();

        await using var first = CreateConnection(factory);
        await using var second = CreateConnection(factory);
        var roundStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var updates = Channel.CreateUnbounded<object>();

        first.On("roundStarted", () => roundStarted.TrySetResult());
        second.On("roundStarted", () => roundStarted.TrySetResult());

        await first.StartAsync();
        await second.StartAsync();
        await roundStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));

        DiagnosticsSnapshot snapshot = null;
        for (var attempt = 0; attempt < 20; attempt++)
        {
            snapshot = await client.GetFromJsonAsync<DiagnosticsSnapshot>("/diagnostics");
            if (snapshot.RoundState == "InProgress" && snapshot.Players.Count == 2)
            {
                break;
            }

            await Task.Delay(100);
        }

        Assert.NotNull(snapshot);
        Assert.Equal("InProgress", snapshot.RoundState);
        Assert.Equal(2, snapshot.Players.Count);
        Assert.All(snapshot.Players, player => Assert.True(player.IsAlive));
        Assert.Contains(snapshot.Players, player => player.X == 1 && player.Y == 1);
        Assert.Contains(snapshot.Players, player => player.X == 13 && player.Y == 1);
    }

    private static HubConnection CreateConnection(WebApplicationFactory<Program> factory) =>
        new HubConnectionBuilder()
            .WithUrl(new Uri(factory.Server.BaseAddress, "game"), options =>
            {
                options.HttpMessageHandlerFactory = _ => factory.Server.CreateHandler();
                options.Transports = HttpTransportType.LongPolling;
            })
            .Build();
}
