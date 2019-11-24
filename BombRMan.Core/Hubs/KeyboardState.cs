using System;
using System.Collections.Generic;
using System.Text;

namespace BombRMan.Hubs
{
    public class KeyboardState
    {
        private readonly Dictionary<Keys, bool> _keyState;
        public int Id { get; }
        public long Time { get; }

        public KeyboardState(Dictionary<Keys, bool> keyState, int id, long time)
        {
            _keyState = keyState;
            Id = id;
            Time = time;
        }

        public bool this[Keys key]
        {
            get
            {
                return _keyState[key];
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
}