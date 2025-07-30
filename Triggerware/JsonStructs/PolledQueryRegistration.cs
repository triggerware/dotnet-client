using System.Text.Json.Serialization;

namespace Triggerware.JsonStructs;

public class PolledQueryRegistration
{
    [JsonPropertyName("handle")] public ulong Handle { get; set; }

    [JsonPropertyName("signature")] public SignatureElement[] Signature { get; set; }
}