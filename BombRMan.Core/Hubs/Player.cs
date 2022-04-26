using System.Drawing;

namespace BombRMan.Hubs;

public class Player
{
    private static readonly Dictionary<(int, int), Point[]> _hitTargets = GetAllHitTargets();

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

    public double LastProcessedTime { get; set; }

    public void Update(in KeyboardState input)
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
        var sourceLeft = effectiveX * map.TileSize;
        var sourceTop = effectiveY * map.TileSize;
        var sourceRect = new RectangleF(sourceLeft, sourceTop, map.TileSize, map.TileSize);
        Span<Point> collisions = stackalloc Point[6];
        var collisionsSize = 0;
        Span<Point> possible = stackalloc Point[6];
        var possibleSize = 0;

        foreach (var t in _hitTargets[(DirectionX, DirectionY)])
        {
            int targetX = actualX + t.X,
                targetY = actualY + t.Y;

            var targetRect = new RectangleF(targetX * map.TileSize, targetY * map.TileSize, map.TileSize, map.TileSize);
            var movable = map.Movable(targetX, targetY);
            var intersects = sourceRect.IntersectsWith(targetRect);

            if (!movable && intersects)
            {
                collisions[collisionsSize++] = new(targetX, targetY);
            }
            else
            {
                possible[possibleSize++] = new(targetX, targetY);
            }
        }

        if (collisionsSize == 0)
        {
            SetDirection(DirectionX, DirectionY);

            X = actualX;
            Y = actualY;

            ExactX = x;
            ExactY = y;
        }
        else
        {
            Span<(int, int, Point)> candidates = stackalloc (int, int, Point)[6];
            var candidatesSize = 0;
            (int, int, Point)? candidate = null;
            var p1 = new Point(actualX + DirectionX, actualY);
            var p2 = new Point(actualX, actualY + DirectionY);
            foreach (var nextMove in possible)
            {
                if (p1.Equals(nextMove))
                {
                    candidates[candidatesSize++] = (DirectionX, 0, p1);
                }

                if (p2.Equals(nextMove))
                {
                    candidates[candidatesSize++] = (0, DirectionY, p2);
                }
            }

            if (candidatesSize == 1)
            {
                candidate = candidates[0];
            }
            else if (candidatesSize == 2)
            {
                int minDistance = int.MaxValue;
                for (int i = 0; i < candidatesSize; ++i)
                {
                    var targetCandidate = candidates[i];
                    var (_, _, point) = targetCandidate;
                    int xs = (ExactX - point.X * GameState.POWER);
                    int ys = (ExactY - point.Y * GameState.POWER);
                    int distance = xs * xs + ys * ys;

                    if (distance < minDistance)
                    {
                        minDistance = distance;
                        candidate = targetCandidate;
                    }
                }
            }

            if (candidate is { } c)
            {
                var (cx, cy, point) = c;
                var diffX = point.X * GameState.POWER - ExactX;
                var diffY = point.Y * GameState.POWER - ExactY;
                var absX = Math.Abs(diffX);
                var absY = Math.Abs(diffY);

                int effectiveDirectionX;
                if (absX == 100)
                {
                    effectiveDirectionX = 0;
                }
                else
                {
                    effectiveDirectionX = Math.Sign(diffX);
                }

                int effectiveDirectionY;
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
                    effectiveDirectionX = cx;
                    effectiveDirectionY = cy;
                }

                SetDirection(effectiveDirectionX, effectiveDirectionY);

                ExactX += GameState.DELTA * effectiveDirectionX;
                X = actualX;

                ExactY += GameState.DELTA * effectiveDirectionY;
                Y = actualY;
            }
            else
            {
                var diffY = collisions[0].Y * GameState.POWER - ExactY;
                var diffX = collisions[0].X * GameState.POWER - ExactX;
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

    private static Dictionary<(int, int), Point[]> GetAllHitTargets()
    {
        var eastTargets = new Point[] { new(1, -1), new(1, 0), new(1, 1) };
        var westTargets = new Point[] { new(-1, -1), new(-1, 0), new(-1, 1) };
        var northTargets = new Point[] { new(-1, -1), new(0, -1), new(1, -1) };
        var southTargets = new Point[] { new(-1, 1), new(0, 1), new(1, 1) };

        Point[] GetXHitTargets(int directionX)
        {
            return directionX switch
            {
                1 => eastTargets,
                -1 => westTargets,
                _ => Array.Empty<Point>()
            };
        }

        Point[] GetYHitTargets(int directionY)
        {
            return directionY switch
            {
                1 => southTargets,
                -1 => northTargets,
                _ => Array.Empty<Point>()
            };
        }

        var possibleValues = new[] { -1, 0, 1 };

        return (from x in possibleValues
                from y in possibleValues
                let points = GetXHitTargets(x).Concat(GetYHitTargets(y)).ToArray()
                select (x, y, points))
                .ToDictionary(k => (k.x, k.y), p => p.points);
    }
}
