using System.Buffers;
using System.Text.Json;
using BombRMan.Hubs;
using Xunit;

namespace BombRMan.Core.Tests;

public class KeyboardStateConverterTests
{
    [Fact]
    public void ReadClearsUnusedPooledEntries()
    {
        var options = CreateOptions();
        var states = JsonSerializer.Deserialize<KeyboardState[]>(
            """[{"id":7,"time":12.5,"keyState":[2]}]""",
            options)!;

        try
        {
            Assert.Equal(7, states[0].Id);
            Assert.Equal(12.5, states[0].Time);
            Assert.Equal(2u, states[0].KeyState[0]);
            Assert.Null(states[1].KeyState);
        }
        finally
        {
            states[0].Dispose();
            ArrayPool<KeyboardState>.Shared.Return(states, clearArray: true);
        }
    }

    [Fact]
    public void ReadRejectsOversizedInputBatches()
    {
        var options = CreateOptions();
        var payload = "[" + string.Join(
            ',',
            Enumerable.Repeat("""{"id":1,"time":0,"keyState":[0]}""", 65)) + "]";

        Assert.Throws<InvalidDataException>(
            () => JsonSerializer.Deserialize<KeyboardState[]>(payload, options));
    }

    private static JsonSerializerOptions CreateOptions()
    {
        var options = new JsonSerializerOptions();
        options.Converters.Add(new KeyboardStateConverter());
        return options;
    }
}
