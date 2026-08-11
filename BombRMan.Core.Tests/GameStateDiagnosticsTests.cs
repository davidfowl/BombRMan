using System.Buffers;
using System.Collections.Concurrent;
using System.Linq;
using BombRMan.Hubs;
using Xunit;

namespace BombRMan.Core.Tests;

public class GameStateDiagnosticsTests
{
    [Fact]
    public void TrimQueueDropsOldestEntriesBeyondMaxSize()
    {
        var queue = new ConcurrentQueue<KeyboardState>();
        for (int i = 0; i < 5; i++)
        {
            queue.Enqueue(CreateInput(i));
        }

        var dropped = GameState.TrimQueue(queue, maxSize: 3);

        Assert.Equal(2, dropped);
        Assert.Equal(3, queue.Count);

        // The oldest entries (ids 0 and 1) should have been the ones dropped.
        Assert.Equal(2, queue.First().Id);
    }

    [Fact]
    public void TrimQueueDoesNothingWhenUnderLimit()
    {
        var queue = new ConcurrentQueue<KeyboardState>();
        queue.Enqueue(CreateInput(1));
        queue.Enqueue(CreateInput(2));

        var dropped = GameState.TrimQueue(queue, maxSize: 5);

        Assert.Equal(0, dropped);
        Assert.Equal(2, queue.Count);
    }

    [Theory]
    [InlineData(0, 16, 0, 0, false)]
    [InlineData(15, 16, 0, 15, false)]
    [InlineData(16, 16, 1, 0, false)]
    [InlineData(32, 16, 2, 0, true)]
    [InlineData(40, 16, 2, 8, true)]
    public void ComputeFrameAdvanceReportsIterationsRemainderAndOverrun(
        int delta, int frameTicks, int expectedIterations, int expectedRemaining, bool expectedOverrun)
    {
        var (iterations, remaining, isOverrun) = GameState.ComputeFrameAdvance(delta, frameTicks);

        Assert.Equal(expectedIterations, iterations);
        Assert.Equal(expectedRemaining, remaining);
        Assert.Equal(expectedOverrun, isOverrun);
    }

    private static KeyboardState CreateInput(int id)
    {
        var keyState = ArrayPool<uint>.Shared.Rent(8);
        Array.Clear(keyState);
        return new KeyboardState(keyState, id, 0);
    }
}
