namespace BombRMan.Hubs;

internal sealed class PlayerState : IDisposable
{
    public string PlayerId { get; set; }
    public PlayerInputBuffer Inputs { get; } = new();
    public Player Player { get; set; }

    public void Dispose() => Inputs.Dispose();
}
