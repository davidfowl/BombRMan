using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;

namespace BombRMan.Hubs
{
    public class Player
    {
        private static readonly Point[] EastTargets = new Point[] { new Point(1, -1), new Point(1, 0), new Point(1, 1) };
        private static readonly Point[] WestTargets = new Point[] { new Point(-1, -1), new Point(-1, 0), new Point(-1, 1) };
        private static readonly Point[] NorthTargets = new Point[] { new Point(-1, -1), new Point(0, -1), new Point(1, -1) };
        private static readonly Point[] SouthTargets = new Point[] { new Point(-1, 1), new Point(0, 1), new Point(1, 1) };

        public int X { get; set; }
        public int Y { get; set; }
        public int ExactX { get; set; }
        public int ExactY { get; set; }
        public int DirectionX { get; set; }
        public int DirectionY { get; set; }

        internal GameState GameState { get; set; }

        public Direction Direction { get; set; }

        public int Index { get; set; }

        public int LastProcessed { get; set; }

        public long LastProcessedTime { get; set; }

        public void Update(KeyboardState input)
        {
            LastProcessed = input.Id;
            LastProcessedTime = input.Time;

            int x = ExactX,
                y = ExactY;

            if (!input[Keys.UP])
            {
                DirectionY = 0;
            }

            if (!input[Keys.DOWN])
            {
                DirectionY = 0;
            }

            if (!input[Keys.LEFT])
            {
                DirectionX = 0;
            }

            if (!input[Keys.RIGHT])
            {
                DirectionX = 0;
            }

            if (input[Keys.UP])
            {
                DirectionY = -1;
            }

            if (input[Keys.DOWN])
            {
                DirectionY = 1;
            }

            if (input[Keys.LEFT])
            {
                DirectionX = -1;
            }

            if (input[Keys.RIGHT])
            {
                DirectionX = 1;
            }

            SetDirection(DirectionX, DirectionY);

            x += DirectionX * GameState.DELTA;
            y += DirectionY * GameState.DELTA;

            MoveExact(x, y);
        }

        private void MoveExact(int x, int y)
        {
            var map = GameState.Map;

            float effectiveX = x / (GameState.POWER * 1f),
                  effectiveY = y / (GameState.POWER * 1f);

            var actualX = (int)Math.Floor((float)(x + (GameState.POWER / 2)) / GameState.POWER);
            var actualY = (int)Math.Floor((float)(y + (GameState.POWER / 2)) / GameState.POWER);
            var targets = GetHitTargets();
            var sourceLeft = effectiveX * map.TileSize;
            var sourceTop = effectiveY * map.TileSize;
            var sourceRect = new RectangleF(sourceLeft, sourceTop, map.TileSize, map.TileSize);
            var collisions = new List<Point>();
            var possible = new List<Point>();

            foreach (var t in targets)
            {
                int targetX = actualX + t.X,
                    targetY = actualY + t.Y;

                var targetRect = new RectangleF(targetX * map.TileSize, targetY * map.TileSize, map.TileSize, map.TileSize);
                var movable = map.Movable(targetX, targetY);
                var intersects = sourceRect.IntersectsWith(targetRect);

                if (!movable && intersects)
                {
                    collisions.Add(new Point(targetX, targetY));
                }
                else
                {
                    possible.Add(new Point(targetX, targetY));
                }
            }

            if (collisions.Count == 0)
            {
                SetDirection(DirectionX, DirectionY);

                X = actualX;
                Y = actualY;

                ExactX = x;
                ExactY = y;
            }
            else
            {
                var candidates = new List<Tuple<int, int, Point>>();
                Tuple<int, int, Point> candidate = null;
                var p1 = new Point(actualX + DirectionX, actualY);
                var p2 = new Point(actualX, actualY + DirectionY);
                foreach (var nextMove in possible)
                {
                    if (p1.Equals(nextMove))
                    {
                        candidates.Add(Tuple.Create(DirectionX, 0, p1));
                    }

                    if (p2.Equals(nextMove))
                    {
                        candidates.Add(Tuple.Create(0, DirectionY, p2));
                    }
                }

                if (candidates.Count == 1)
                {
                    candidate = candidates[0];
                }
                else if (candidates.Count == 2)
                {
                    int minDistance = Int32.MaxValue;
                    for (int i = 0; i < candidates.Count; ++i)
                    {
                        var targetCandidate = candidates[i];
                        int xs = (ExactX - candidates[i].Item3.X * GameState.POWER);
                        int ys = (ExactY - candidates[i].Item3.Y * GameState.POWER);
                        int distance = xs * xs + ys * ys;

                        if (distance < minDistance)
                        {
                            minDistance = distance;
                            candidate = targetCandidate;
                        }
                    }
                }

                if (candidate != null)
                {
                    var diffX = candidate.Item3.X * GameState.POWER - ExactX;
                    var diffY = candidate.Item3.Y * GameState.POWER - ExactY;
                    var absX = Math.Abs(diffX);
                    var absY = Math.Abs(diffY);
                    int effectiveDirectionX = 0;
                    int effectiveDirectionY = 0;

                    if (absX == 100)
                    {
                        effectiveDirectionX = 0;
                    }
                    else
                    {
                        effectiveDirectionX = Math.Sign(diffX);
                    }

                    if (absY == 100)
                    {
                        effectiveDirectionY = 0;
                    }
                    else
                    {
                        effectiveDirectionY = Math.Sign(diffY);
                    }

                    if (effectiveDirectionX == 0 && effectiveDirectionY == 0)
                    {
                        effectiveDirectionX = candidate.Item1;
                        effectiveDirectionY = candidate.Item2;
                    }

                    SetDirection(effectiveDirectionX, effectiveDirectionY);

                    ExactX += GameState.DELTA * effectiveDirectionX;
                    X = actualX;

                    ExactY += GameState.DELTA * effectiveDirectionY;
                    Y = actualY;
                }
                else
                {
                    var diffY = (collisions[0].Y * GameState.POWER - ExactY);
                    var diffX = (collisions[0].X * GameState.POWER - ExactX);
                    var absX = Math.Abs(diffX);
                    var absY = Math.Abs(diffY);
                    int effectiveDirectionX = 0;
                    int effectiveDirectionY = 0;

                    if (absX >= 35 && absX < 100)
                    {
                        effectiveDirectionX = -Math.Sign(diffX);
                    }

                    if (absY >= 35 && absY < 100)
                    {
                        effectiveDirectionY = -Math.Sign(diffY);
                    }

                    SetDirection(effectiveDirectionX, effectiveDirectionY);

                    ExactX += GameState.DELTA * effectiveDirectionX;
                    ExactY += GameState.DELTA * effectiveDirectionY;
                }
            }
        }

        private void SetDirection(int x, int y)
        {
            if (x == -1)
            {
                Direction = Direction.WEST;
            }
            else if (x == 1)
            {
                Direction = Direction.EAST;
            }

            if (y == -1)
            {
                Direction = Direction.NORTH;
            }
            else if (y == 1)
            {
                Direction = Direction.SOUTH;
            }
        }

        private Point[] GetHitTargets()
        {
            return GetXHitTargets().Concat(GetYHitTargets()).ToArray();
        }

        private Point[] GetXHitTargets()
        {
            if (DirectionX == 1)
            {
                return EastTargets;
            }
            else if (DirectionX == -1)
            {
                return WestTargets;
            }

            return new Point[0];
        }

        private Point[] GetYHitTargets()
        {
            if (DirectionY == -1)
            {
                return NorthTargets;
            }
            else if (DirectionY == 1)
            {
                return SouthTargets;
            }

            return new Point[0];
        }
    }
}