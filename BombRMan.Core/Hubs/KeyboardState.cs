using System.Buffers;
using System.Text;

namespace BombRMan.Hubs;

public readonly struct KeyboardState : IDisposable
{
    public uint[] KeyState { get; }
    public int Id { get; }
    public double Time { get; }

    public KeyboardState(uint[] keyState, int id, double time)
    {
        KeyState = keyState;
        Id = id;
        Time = time;
    }

    public bool this[Keys key]
    {
        get
        {
            var index = (int)key >> 5;
            var bit = (uint)(1 << ((int)key & 0x1f));
            return (KeyState[index] & bit) == bit;
        }
    }

    public override string ToString()
    {
        var sb = new StringBuilder();
        foreach (var value in Enum.GetValues(typeof(Keys)))
        {
            if (sb.Length > 0)
            {
                sb.Append(", ");
            }

            sb.Append(Enum.GetName(typeof(Keys), value))
              .Append(" = ")
              .Append(this[(Keys)value]);
        }

        sb.AppendLine();

        return sb.ToString();
    }

    public void Dispose()
    {
        ArrayPool<uint>.Shared.Return(KeyState);
    }
}
