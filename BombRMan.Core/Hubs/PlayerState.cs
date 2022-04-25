using System.Collections.Concurrent;

namespace BombRMan.Hubs;

public class PlayerState
{
    public string PlayerId { get; set; }
    public ConcurrentQueue<KeyboardState> Inputs { get; set; }
    public Player Player { get; set; }
}
