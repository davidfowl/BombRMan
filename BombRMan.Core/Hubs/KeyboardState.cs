using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace BombRMan.Hubs;

//[JsonConverter(typeof(KeyboardStateConverter))]
public class KeyboardState
{
    public Dictionary<Keys, bool> KeyState { get; }
    public int Id { get; }
    public double Time { get; }

    public KeyboardState(Dictionary<Keys, bool> keyState, int id, double time)
    {
        KeyState = keyState;
        Id = id;
        Time = time;
    }

    public bool this[Keys key]
    {
        get
        {
            return KeyState[key];
        }
    }

    public bool Empty
    {
        get
        {
            return !this[Keys.A] &&
                   !this[Keys.D] &&
                   !this[Keys.DOWN] &&
                   !this[Keys.LEFT] &&
                   !this[Keys.RIGHT] &&
                   !this[Keys.UP] &&
                   !this[Keys.P];
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
}