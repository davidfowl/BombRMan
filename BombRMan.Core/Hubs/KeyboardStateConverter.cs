using System.Buffers;
using System.Buffers.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace BombRMan.Hubs;

internal class KeyboardStateConverter : JsonConverter<KeyboardState[]>
{
    private static readonly JsonEncodedText IdPropertyName = JsonEncodedText.Encode("id");
    private static readonly JsonEncodedText TimePropertyName = JsonEncodedText.Encode("time");
    private static readonly JsonEncodedText KeyStatePropertyName = JsonEncodedText.Encode("keyState");

    public override KeyboardState[] Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var keyboardStates = ArrayPool<KeyboardState>.Shared.Rent(1);
        var count = 0;

        while (reader.Read() && reader.TokenType != JsonTokenType.EndArray)
        {
            int id = 0;
            double time = 0;
            var keyState = ArrayPool<uint>.Shared.Rent(8);

            while (reader.Read() && reader.TokenType != JsonTokenType.EndObject)
            {
                if (reader.ValueTextEquals(IdPropertyName.EncodedUtf8Bytes))
                {
                    reader.Read();
                    id = reader.GetInt32();
                }
                else if (reader.ValueTextEquals(TimePropertyName.EncodedUtf8Bytes))
                {
                    reader.Read();
                    time = reader.GetDouble();
                }
                else if (reader.ValueTextEquals(KeyStatePropertyName.EncodedUtf8Bytes))
                {
                    reader.Read();
                    if (reader.TokenType != JsonTokenType.StartArray)
                    {
                        throw new InvalidDataException();
                    }

                    // This is an array of flags
                    var index = 0;
                    while (reader.Read() && reader.TokenType != JsonTokenType.EndArray)
                    {
                        _ = Utf8Parser.TryParse(reader.ValueSpan, out uint flags, out _);

                        keyState[index++] = flags;
                    }
                }
            }

            keyboardStates[count++] = new(keyState, id, time);

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
        throw new NotSupportedException();
    }
}
