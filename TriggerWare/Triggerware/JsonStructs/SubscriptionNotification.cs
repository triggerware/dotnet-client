using System.Text.Json.Serialization;

namespace Triggerware.JsonStructs;

public class SubscriptionNotification<T>
{
    [JsonPropertyName("update#")]
    public long UpdateNumber { get; set; }
    [JsonPropertyName("label")]
    public string Label { get; set; }
    [JsonPropertyName("tuple")]
    public T Tuple { get; set; }
}