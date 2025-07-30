using System.Text.Json.Serialization;

namespace Triggerware.JsonStructs;

public class CombinedSubscriptionNotification
{
    [JsonPropertyName("update#")] public ulong UpdateNumber { get; set; }

    [JsonPropertyName("matches")] public SubscriptionMatch<object>[] Matches { get; set; }
}