using System.Text.Json.Serialization;

namespace Triggerware.JsonStructs;

public class ExecuteQueryResult<T>
{
    [JsonPropertyName("handle")] public long? Handle { get; set; }

    [JsonPropertyName("batch")] public Batch<T> Batch { get; set; }

    [JsonPropertyName("signature")] public SignatureElement[] Signature { get; set; }
}