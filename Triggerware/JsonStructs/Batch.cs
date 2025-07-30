using System.Text.Json.Serialization;

namespace Triggerware.JsonStructs;

public class Batch<T>
{
    [JsonPropertyName("count")] public uint Count { get; set; }

    [JsonPropertyName("tuples")] public T[] Rows { get; set; } = [];

    [JsonPropertyName("exhausted")] public bool Exhausted { get; set; }
}