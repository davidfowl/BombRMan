using BombRMan.Hubs;
using Xunit;

namespace BombRMan.Core.Tests;

public class MapTests
{
    private const string RawData = "222" + "203" + "222";

    [Fact]
    public void DestroyingABrickThenResettingRestoresOriginalLayout()
    {
        var map = new Map(RawData, 3, 3, 32);

        Assert.Equal(Tile.BRICK, map[2, 1]);

        map[2, 1] = Tile.GRASS;
        Assert.Equal(Tile.GRASS, map[2, 1]);

        map.Reset();

        Assert.Equal(Tile.BRICK, map[2, 1]);
    }

    [Fact]
    public void SnapshotReflectsCurrentMutatedState()
    {
        var map = new Map(RawData, 3, 3, 32);
        map[2, 1] = Tile.GRASS;

        Assert.Equal("222" + "200" + "222", map.Snapshot());
        Assert.Equal(RawData, map.RawData);
    }
}
