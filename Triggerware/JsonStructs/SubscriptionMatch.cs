using System.Text.Json;
using System.Text.Json.Serialization;

namespace Triggerware.JsonStructs;

public class SubscriptionMatch<T>
{
    [JsonPropertyName("label")] public string Label { get; set; }

    [JsonPropertyName("tuples")] public T[] Tuples { get; set; }
}