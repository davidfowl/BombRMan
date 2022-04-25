using System.Buffers;
using System.Buffers.Text;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.ObjectPool;

namespace BombRMan.Hubs;

public class KeyboardState
{
    private readonly int[] _keyState = new int[32];
    public int Id { get; set; }
    public double Time { get; set; }

    public bool this[Keys key]
    {
        get
        {
            var index = (int)key >> 5;
            var bit = 1 << ((int)key & 0x1f);
            return (_keyState[index] & bit) == bit;
        }
        set
        {
            var index = (int)key >> 5;
            var bit = 1 << ((int)key & 0x1f);
            if (value)
            {
                _keyState[index] |= bit;
            }
            else
            {
                _keyState[index] &= ~bit;
            }
        }
    }

    public bool Empty
    {
        get
        {
            return !this[Keys.A] &&
                   !this[Keys.D] &&
                   !this[Keys.DOWN] &&
                   !this[Keys.LEFT] &&
                   !this[Keys.RIGHT] &&
                   !this[Keys.UP] &&
                   !this[Keys.P];
        }
    }

    public override string ToString()
    {
        var sb = new StringBuilder();
        foreach (var value in Enum.GetValues(typeof(Keys)))
        {
            if (sb.Length > 0)
            {
                sb.Append(", ");
            }

            sb.Append(Enum.GetName(typeof(Keys), value))
              .Append(" = ")
              .Append(this[(Keys)value]);
        }

        sb.AppendLine();

        return sb.ToString();
    }

    public void Reset()
    {
        _keyState.AsSpan().Clear();
    }
}

class KeyboardStateConverter : JsonConverter<KeyboardState[]>
{
    private static readonly JsonEncodedText IdPropertyName = JsonEncodedText.Encode("id");
    private static readonly JsonEncodedText TimePropertyName = JsonEncodedText.Encode("time");
    private static readonly JsonEncodedText KeyStatePropertyName = JsonEncodedText.Encode("keyState");

    private readonly ObjectPool<KeyboardState> _pool;

    public KeyboardStateConverter(ObjectPool<KeyboardState> pool)
    {
        _pool = pool;
    }

    public override KeyboardState[] Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var keyboardStates = ArrayPool<KeyboardState>.Shared.Rent(1);
        var count = 0;

        while (reader.Read() && reader.TokenType != JsonTokenType.EndArray)
        {
            var keyboardState = _pool.Get();

            while (reader.Read() && reader.TokenType != JsonTokenType.EndObject)
            {
                if (reader.ValueTextEquals(IdPropertyName.EncodedUtf8Bytes))
                {
                    reader.Read();
                    keyboardState.Id = reader.GetInt32();
                }
                else if (reader.ValueTextEquals(TimePropertyName.EncodedUtf8Bytes))
                {
                    reader.Read();
                    keyboardState.Time = reader.GetDouble();
                }
                else if (reader.ValueTextEquals(KeyStatePropertyName.EncodedUtf8Bytes))
                {
                    reader.Read();
                    if (reader.TokenType != JsonTokenType.StartObject)
                    {
                        throw new InvalidDataException();
                    }

                    // This is a flat object 
                    //  { "keyCode": true/false }
                    while (reader.Read() && reader.TokenType != JsonTokenType.EndObject)
                    {
                        // Property name
                        _ = Utf8Parser.TryParse(reader.ValueSpan, out int keyCode, out _);

                        reader.Read();
                        // Property value

                        keyboardState[(Keys)keyCode] = reader.GetBoolean();
                    }
                }
            }

            keyboardStates[count++] = keyboardState;

            if (count >= keyboardStates.Length)
            {
                // Create the new array
                var newArray = ArrayPool<KeyboardState>.Shared.Rent(keyboardStates.Length * 2);

                // Copy the old array to the new array
                keyboardStates.AsSpan().CopyTo(newArray);

                // Return the old array
                ArrayPool<KeyboardState>.Shared.Return(keyboardStates);

                keyboardStates = newArray;
            }
        }

        return keyboardStates;
    }

    public override void Write(Utf8JsonWriter writer, KeyboardState[] value, JsonSerializerOptions options)
    {
        JsonSerializer.Serialize(writer, value, options);
    }
}

class KeyboardStatePolicyProvider : IPooledObjectPolicy<KeyboardState>
{
    public KeyboardState Create()
    {
        return new();
    }

    public bool Return(KeyboardState obj)
    {
        obj.Reset();

        return true;
    }
}
