using System.Collections.Concurrent;

namespace BombRMan.Hubs;

public class PlayerState
{
    public string PlayerId { get; set; }
    public ConcurrentQueue<KeyboardState> Inputs { get; set; }
    public Player Player { get; set; }

    /// <summary>
    /// Tracks whether the bomb key was held on the last processed input, so bomb placement
    /// can be triggered only on the rising edge (key press) rather than every tick it's held.
    /// </summary>
    public bool PrevBombKeyDown { get; set; }
}
