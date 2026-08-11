namespace BombRMan.Hubs;

internal sealed class PlayerState : IDisposable
{
    public string PlayerId { get; set; }
    public PlayerInputBuffer Inputs { get; } = new();
    public Player Player { get; set; }

    public void Dispose() => Inputs.Dispose();
    /// <summary>
    /// Tracks whether the bomb key was held on the last processed input, so bomb placement
    /// can be triggered only on the rising edge (key press) rather than every tick it's held.
    /// </summary>
    public bool PrevBombKeyDown { get; set; }
}
