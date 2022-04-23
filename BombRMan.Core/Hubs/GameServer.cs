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
        await Clients.Caller.SendAsync("initializeMap", _gameState.Map.RawData);

        if (_gameState.TryAddPlayer(Context.ConnectionId, out var player))
        {
            await Clients.Caller.SendAsync("initializePlayer", player);
        }

        await Clients.All.SendAsync("initialize", _gameState.ActivePlayers);
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
