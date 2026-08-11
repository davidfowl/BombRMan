using System.Drawing;

namespace BombRMan.Hubs;

/// <summary>
/// Pure, testable logic for computing which tiles a bomb's blast covers.
/// </summary>
public static class ExplosionCalculator
{
    private static readonly Point[] Directions = { new(1, 0), new(-1, 0), new(0, 1), new(0, -1) };

    /// <summary>
    /// Returns the tiles affected by a bomb exploding at (originX, originY) with the given power,
    /// including the origin tile itself. Propagation along each of the 4 cardinal directions stops
    /// at the first WALL tile (not included) or the first BRICK tile (included, then stops), or once
    /// <paramref name="power"/> tiles have been traversed.
    /// </summary>
    public static List<Point> GetBlastTiles(Map map, int originX, int originY, int power)
    {
        var tiles = new List<Point> { new(originX, originY) };

        foreach (var d in Directions)
        {
            for (int i = 1; i <= power; i++)
            {
                int x = originX + d.X * i;
                int y = originY + d.Y * i;

                if (y < 0 || y >= map.Height || x < 0 || x >= map.Width)
                {
                    break;
                }

                var tile = map[x, y];

                if (tile == Tile.WALL)
                {
                    break;
                }

                tiles.Add(new Point(x, y));

                if (tile == Tile.BRICK)
                {
                    // The blast destroys the brick but doesn't travel through it.
                    break;
                }
            }
        }

        return tiles;
    }
}
