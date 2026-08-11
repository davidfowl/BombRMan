using System.Buffers;
using System.Buffers.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace BombRMan.Hubs;

internal class KeyboardStateConverter : JsonConverter<KeyboardState[]>
{
    private const int KeyStateLength = 8;
    private const int MaxInputBatchLength = 64;
    private static readonly JsonEncodedText IdPropertyName = JsonEncodedText.Encode("id");
    private static readonly JsonEncodedText TimePropertyName = JsonEncodedText.Encode("time");
    private static readonly JsonEncodedText KeyStatePropertyName = JsonEncodedText.Encode("keyState");

    public override KeyboardState[] Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var keyboardStates = ArrayPool<KeyboardState>.Shared.Rent(MaxInputBatchLength);
        Array.Clear(keyboardStates);
        var count = 0;

        try
        {
            while (reader.Read() && reader.TokenType != JsonTokenType.EndArray)
            {
                if (count == MaxInputBatchLength)
                {
                    throw new InvalidDataException();
                }

                int id = 0;
                double time = 0;
                var keyState = ArrayPool<uint>.Shared.Rent(KeyStateLength);
                var accepted = false;

                try
                {
                    Array.Clear(keyState);

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

                            var index = 0;
                            while (reader.Read() && reader.TokenType != JsonTokenType.EndArray)
                            {
                                if (index >= keyState.Length ||
                                    !Utf8Parser.TryParse(reader.ValueSpan, out uint flags, out _))
                                {
                                    throw new InvalidDataException();
                                }

                                keyState[index++] = flags;
                            }
                        }
                    }

                    keyboardStates[count++] = new(keyState, id, time);
                    accepted = true;
                }
                finally
                {
                    if (!accepted)
                    {
                        ArrayPool<uint>.Shared.Return(keyState);
                    }
                }
            }

            return keyboardStates;
        }
        catch
        {
            foreach (var keyboardState in keyboardStates.AsSpan(0, count))
            {
                keyboardState.Dispose();
            }

            ArrayPool<KeyboardState>.Shared.Return(keyboardStates, clearArray: true);
            throw;
        }
    }

    public override void Write(Utf8JsonWriter writer, KeyboardState[] value, JsonSerializerOptions options)
    {
        throw new NotSupportedException();
    }
}
