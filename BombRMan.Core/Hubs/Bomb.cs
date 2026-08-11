namespace BombRMan.Hubs;

public class Bomb
{
    public int Id { get; set; }
    public int OwnerIndex { get; set; }
    public int X { get; set; }
    public int Y { get; set; }
    public int Power { get; set; }
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
