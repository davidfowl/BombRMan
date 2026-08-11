using System.Buffers;
using BombRMan.Hubs;
using Xunit;

namespace BombRMan.Core.Tests;

public class PlayerTests
{
    [Fact]
    public void UpdateMovesPlayerByOneStepOnOpenTile()
    {
        var player = CreatePlayer(100, 100);
        var map = CreateMap();
        var input = CreateInput(Keys.RIGHT);

        try
        {
            player.Update(input, map);

            Assert.Equal(110, player.ExactX);
            Assert.Equal(100, player.ExactY);
            Assert.Equal(Direction.EAST, player.Direction);
        }
        finally
        {
            input.Dispose();
        }
    }

    [Fact]
    public void UpdateDoesNotMoveThroughWall()
    {
        var player = CreatePlayer(100, 100);
        var map = CreateMap();
        var input = CreateInput(Keys.LEFT);

        try
        {
            player.Update(input, map);

            Assert.Equal(100, player.ExactX);
            Assert.Equal(100, player.ExactY);
        }
        finally
        {
            input.Dispose();
        }
    }

    [Fact]
    public void UpdateContinuesMovingFromTheLastAppliedInput()
    {
        var player = CreatePlayer(100, 100);
        var map = CreateMap();
        var input = CreateInput(Keys.RIGHT);

        try
        {
            player.ApplyInput(input);
            player.Update(map);
            player.Update(map);

            Assert.Equal(120, player.ExactX);
            Assert.Equal(100, player.ExactY);
        }
        finally
        {
            input.Dispose();
        }
    }

    [Fact]
    public void OpposingInputsCancelTheirAxis()
    {
        var player = CreatePlayer(100, 100);
        var map = CreateMap();
        var input = CreateInput(Keys.LEFT, Keys.RIGHT, Keys.UP, Keys.DOWN);

        try
        {
            player.Update(input, map);

            Assert.Equal(100, player.ExactX);
            Assert.Equal(100, player.ExactY);
            Assert.Equal(0, player.DirectionX);
            Assert.Equal(0, player.DirectionY);
        }
        finally
        {
            input.Dispose();
        }
    }

    private static Player CreatePlayer(int exactX, int exactY) => new()
    {
        ExactX = exactX,
        ExactY = exactY,
        X = exactX / GameState.POWER,
        Y = exactY / GameState.POWER
    };

    private static Map CreateMap() => new("22222" + "20002" + "22222", 5, 3, 32);

    private static KeyboardState CreateInput(params Keys[] keys)
    {
        var keyState = ArrayPool<uint>.Shared.Rent(8);
        Array.Clear(keyState);

        foreach (var key in keys)
        {
            var index = (int)key >> 5;
            var bit = 1u << ((int)key & 0x1f);
            keyState[index] |= bit;
        }

        return new KeyboardState(keyState, 1, 0);
    }
}
