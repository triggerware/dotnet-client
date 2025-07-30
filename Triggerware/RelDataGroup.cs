using System.Text.Json.Serialization;
using System.Text.Json;

namespace Triggerware;

[JsonConverter(typeof(RelDataGroupConverter))]
public class RelDataGroup
{
    public string Name { get; set; }
    public string Symbol { get; set; }
    public List<RelDataElement> Elements { get; set; }
}

public class RelDataGroupConverter : JsonConverter<RelDataGroup>
{
    public override RelDataGroup? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var group = new RelDataGroup();

        if (reader.TokenType != JsonTokenType.StartArray) throw new JsonException("Expected StartArray token");

        reader.Read();
        group.Name = reader.GetString() ?? throw new JsonException("Expected string for Name");

        reader.Read();
        group.Symbol = reader.GetString() ?? throw new JsonException("Expected string for Symbol");

        reader.Read();

        group.Elements = [];
        while (reader.TokenType != JsonTokenType.EndArray)
        {
            var element = JsonSerializer.Deserialize<RelDataElement>(ref reader, options) ??
                          throw new JsonException("Expected RelDataElement");
            group.Elements.Add(element);
            reader.Read();
        }

        return group;
    }

    public override void Write(Utf8JsonWriter writer, RelDataGroup value, JsonSerializerOptions options)
    {
        writer.WriteStartArray();
        writer.WriteStringValue(value.Name);
        writer.WriteStringValue(value.Symbol);
        JsonSerializer.Serialize(writer, value.Elements, options);
        writer.WriteEndArray();
    }
}

// rel data group thing
[JsonConverter(typeof(RelDataElementConverter))]
public class RelDataElement
{
    // Consider making these properties public or internal if needed.
    public string Name { get; set; }
    public string[] SignatureNames { get; set; }
    public string[] SignatureTypes { get; set; }
    public string Usage { get; set; }
    public string[] NoIdea { get; set; }
    public string Description { get; set; }
}

public class RelDataElementConverter : JsonConverter<RelDataElement>
{
    public override RelDataElement Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.StartArray) throw new JsonException("Expected StartArray token");

        var element = new RelDataElement();

        reader.Read();
        element.Name = reader.GetString() ?? throw new JsonException("Expected string for Name");

        reader.Read();
        element.SignatureNames = JsonSerializer.Deserialize<string[]>(ref reader, options) ??
                                 throw new JsonException("Expected string[] for SignatureNames");

        reader.Read();
        element.SignatureTypes = JsonSerializer.Deserialize<string[]>(ref reader, options) ??
                                 throw new JsonException("Expected string[] for SignatureTypes");

        reader.Read();
        element.Usage = reader.GetString() ?? throw new JsonException("Expected string for Usage");

        reader.Read();
        element.NoIdea = JsonSerializer.Deserialize<string[]>(ref reader, options) ??
                         throw new JsonException("Expected a string array");

        reader.Read();
        element.Description = reader.GetString() ?? throw new JsonException("Expected string for Description");

        reader.Read();
        if (reader.TokenType != JsonTokenType.EndArray)
            throw new JsonException("Expected EndArray token");

        return element;
    }

    public override void Write(Utf8JsonWriter writer, RelDataElement value, JsonSerializerOptions options)
    {
        writer.WriteStartArray();
        writer.WriteStringValue(value.Name);
        JsonSerializer.Serialize(writer, value.SignatureNames, options);
        JsonSerializer.Serialize(writer, value.SignatureTypes, options);
        writer.WriteStringValue(value.Usage);
        JsonSerializer.Serialize(writer, value.NoIdea, options);
        writer.WriteStringValue(value.Description);
        writer.WriteEndArray();
    }
}
