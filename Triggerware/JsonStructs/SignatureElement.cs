using System.Text.Json.Serialization;

namespace Triggerware.JsonStructs;

public class SignatureElement
{
    [JsonPropertyName("attribute")] public string Name { get; set; }

    [JsonPropertyName("type")] public string TypeName { get; set; }

    [JsonIgnore]
    public Type[] Types =>
        TypeName switch
        {
            "double" => [typeof(double), typeof(float)],
            "integer" => [typeof(int)],
            "number" => [typeof(decimal), typeof(float), typeof(double), typeof(int)],
            "boolean" => [typeof(bool)],
            "stringcase" => [typeof(string)],
            "stringnocase" => [typeof(string)],
            "stringagnostic" => [typeof(string)],
            "date" => [typeof(DateTime)],
            "time" => [typeof(DateTime)],
            "timestamp" => [typeof(DateTime)],
            "interval" => [typeof(TimeSpan)],
            _ => [typeof(object)]
        };
}