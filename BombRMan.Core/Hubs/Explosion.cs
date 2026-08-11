namespace BombRMan.Hubs;

public class Explosion
{
    public int X { get; set; }
    public int Y { get; set; }
    public int TicksRemaining { get; set; }

    public bool Tick()
    {
        if (TicksRemaining > 0)
        {
            TicksRemaining--;
        }

        return TicksRemaining == 0;
    }
}
