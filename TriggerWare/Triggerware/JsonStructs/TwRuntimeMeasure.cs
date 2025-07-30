using System.Text.Json;
using System.Text.Json.Serialization;

namespace Triggerware;

[JsonConverter(typeof(TwRuntimeMeasureConverter))]
public class TwRuntimeMeasure
{
    [JsonPropertyName("runTime")] public ulong RunTime { get; set; }

    [JsonPropertyName("gcTime")] public ulong GcTime { get; set; }

    [JsonPropertyName("bytes")] public ulong Bytes { get; set; }
}

public class TwRuntimeMeasureConverter : JsonConverter<TwRuntimeMeasure>
{
    public override TwRuntimeMeasure Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.StartArray)
            throw new JsonException("Expected StartArray token");

        reader.Read();
        var runTime = reader.GetUInt64();

        reader.Read();
        var gcTime = reader.GetUInt64();

        reader.Read();
        var bytes = reader.GetUInt64();

        reader.Read();
        if (reader.TokenType != JsonTokenType.EndArray)
            throw new JsonException("Expected EndArray token");

        return new TwRuntimeMeasure { RunTime = runTime, GcTime = gcTime, Bytes = bytes };
    }

    public override void Write(Utf8JsonWriter writer, TwRuntimeMeasure value, JsonSerializerOptions options)
    {
        writer.WriteStartArray();
        writer.WriteNumberValue(value.RunTime);
        writer.WriteNumberValue(value.GcTime);
        writer.WriteNumberValue(value.Bytes);
        writer.WriteEndArray();
    }
}