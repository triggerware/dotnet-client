using Newtonsoft.Json;

namespace TWClients.JsonRpcMessages;

[JsonConverter(typeof(VoidTypeConverter))]
public class VoidType
{
}

public class VoidTypeConverter : JsonConverter<VoidType>
{
    public override void WriteJson(JsonWriter writer, VoidType? value, JsonSerializer serializer)
    {
        writer.WriteNull();
    }

    public override VoidType? ReadJson(JsonReader reader, Type objectType, VoidType? existingValue, bool hasExistingValue,
        JsonSerializer serializer)
    {
        reader.Skip();
        return new VoidType();
    }
}