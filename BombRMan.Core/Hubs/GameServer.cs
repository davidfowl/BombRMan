using Microsoft.AspNetCore.SignalR;

namespace BombRMan.Hubs;

public class GameServer : Hub
{
    private readonly GameState _gameState;

    public GameServer(GameState gameState)
    {
        _gameState = gameState;
    }

    public override async Task OnConnectedAsync()
    {
        await Clients.Caller.SendAsync("initializeMap", _gameState.Map.Snapshot());

        if (_gameState.TryAddPlayer(Context.ConnectionId, out var player))
        {
            await Clients.Caller.SendAsync("initializePlayer", player);
        }

        await Clients.All.SendAsync("initialize", _gameState.ActivePlayers);

        // Sync mid-round state (bombs/explosions/powerups) for a client joining after the round started.
        await Clients.Caller.SendAsync("initializeBombs", _gameState.Bombs);
        await Clients.Caller.SendAsync("initializeExplosions", _gameState.Explosions);
        await Clients.Caller.SendAsync("initializePowerups", _gameState.Powerups);
    }

    public void SendKeys(KeyboardState[] inputs)
    {
        _gameState.SendKeys(Context.ConnectionId, inputs);
    }

    public override async Task OnDisconnectedAsync(Exception exception)
    {
        if (_gameState.TryRemovePlayer(Context.ConnectionId, out var player))
        {
            await Clients.All.SendAsync("playerLeft", player);
        }
    }
}
