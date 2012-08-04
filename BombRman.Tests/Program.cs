using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using BombRMan.Hubs;

namespace BombRman.Tests
{
    class Program
    {
        static void Main(string[] args)
        {
            GameServer.Player p = new GameServer.Player();
            p.X = 1;
            p.Y = 1;
            p.ExactX = 100;
            p.ExactY = 100;
            p.Direction = GameServer.Direction.SOUTH;

            var down = GetDownKeyboardState(GameServer.Keys.DOWN);
            var right = GetDownKeyboardState(GameServer.Keys.RIGHT);
            p.Update(down);
            p.Update(down);
            p.Update(down);
            p.Update(right);

            Console.WriteLine("({0}, {1})", p.X, p.Y);
            Console.WriteLine("({0:0.00}, {1:0.00})", p.ExactX / 100.0, p.ExactY / 100.0);
        }

        private static GameServer.KeyboardState GetDownKeyboardState(GameServer.Keys key)
        {
            var dict = new Dictionary<GameServer.Keys, bool>();
            foreach (var v in Enum.GetValues(typeof(GameServer.Keys)))
            {
                dict[(GameServer.Keys)v] = false;
            }

            dict[key] = true;

            return new GameServer.KeyboardState(dict, 0);
        }
    }
}
