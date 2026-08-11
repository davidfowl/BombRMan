namespace BombRMan.Hubs;

public class Map
{
    private readonly Tile[] _map;

    public Map(string map, int width, int height, int tileSize)
    {
        RawData = map;
        _map = Create(map);
        Width = width;
        Height = height;
        TileSize = tileSize;
    }

    public string RawData { get; }

    private Tile[] Create(string map)
    {
        var tiles = new Tile[map.Length];
        for (int i = 0; i < map.Length; i++)
        {
            tiles[i] = (Tile)((int)map[i] - '0');
        }
        return tiles;
    }

    public Tile this[int x, int y]
    {
        get
        {
            return _map[GetIndex(x, y)];
        }
        set
        {
            _map[GetIndex(x, y)] = value;
        }
    }
    private int GetIndex(int x, int y)
    {
        return (y * Width) + x;
    }

    public int TileSize { get; private set; }
    public int Width { get; private set; }
    public int Height { get; private set; }

    public bool Movable(int x, int y)
    {
        if (y >= 0 && y < Height && x >= 0 && x < Width)
        {
            if (this[x, y] == Tile.GRASS)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Restores every tile back to the original layout described by <see cref="RawData"/>,
    /// undoing any brick destruction from a previous round.
    /// </summary>
    public void Reset()
    {
        for (int i = 0; i < RawData.Length; i++)
        {
            _map[i] = (Tile)((int)RawData[i] - '0');
        }
    }

    /// <summary>
    /// Returns the current tile layout (reflecting any destroyed bricks) encoded the same way
    /// as <see cref="RawData"/>, for syncing clients that join mid-round.
    /// </summary>
    public string Snapshot()
    {
        var chars = new char[_map.Length];
        for (int i = 0; i < _map.Length; i++)
        {
            chars[i] = (char)('0' + (int)_map[i]);
        }
        return new string(chars);
    }
}
