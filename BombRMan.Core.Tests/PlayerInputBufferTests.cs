using System.Buffers;
using BombRMan.Hubs;
using Xunit;

namespace BombRMan.Core.Tests;

public class PlayerInputBufferTests
{
    [Fact]
    public void KeepsOnlyTheNewestAcceptedInput()
    {
        using var buffer = new PlayerInputBuffer();
        var first = CreateInput(1);
        var latest = CreateInput(2);

        Assert.True(buffer.TryAccept(first, 100));
        Assert.True(buffer.TryAccept(latest, 101));
        Assert.True(buffer.TryTakeLatest(out var accepted));

        try
        {
            Assert.Equal(2, accepted.Id);
        }
        finally
        {
            accepted.Dispose();
        }
    }

    [Fact]
    public void RejectsStaleInputIds()
    {
        using var buffer = new PlayerInputBuffer();
        var latest = CreateInput(2);
        var stale = CreateInput(1);

        Assert.True(buffer.TryAccept(latest, 100));
        Assert.False(buffer.TryAccept(stale, 101));
        stale.Dispose();
        Assert.True(buffer.TryTakeLatest(out var accepted));

        try
        {
            Assert.Equal(2, accepted.Id);
        }
        finally
        {
            accepted.Dispose();
        }
    }

    [Fact]
    public void ExpiresTheLastInputAfterTheConfiguredInterval()
    {
        using var buffer = new PlayerInputBuffer();
        var input = CreateInput(1);

        Assert.True(buffer.TryAccept(input, 100));
        Assert.False(buffer.IsExpired(349, 250));
        Assert.True(buffer.IsExpired(350, 250));
    }

    private static KeyboardState CreateInput(int id)
    {
        var keys = ArrayPool<uint>.Shared.Rent(8);
        Array.Clear(keys);
        return new KeyboardState(keys, id, 0);
    }
}
