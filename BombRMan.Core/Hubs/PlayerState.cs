using System.Collections.Concurrent;

namespace BombRMan.Hubs;

public class PlayerState
{
    public ConcurrentQueue<KeyboardState> Inputs { get; set; }
    public Player Player { get; set; }
}
