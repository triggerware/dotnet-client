using System.Text.Json.Serialization;

namespace Triggerware.JsonStructs;

public class PreparedQueryRegistration
{
    
    [JsonPropertyName("handle")] public long Handle { get; set; }

    [JsonPropertyName("inputSignature")] public SignatureElement[] InputSignature { get; set; }

    [JsonPropertyName("signature")] public SignatureElement[] Signature { get; set; }

    [JsonPropertyName("usesNamedParameters")]
    public bool? UsesNamedParameters { get; set; }

    [JsonIgnore]
    public string[] InputSignatureNames => InputSignature.Select(x => x.Name).ToArray();
    
    [JsonIgnore]
    public Type[][] InputSignatureTypes => InputSignature.Select(x => x.Types).ToArray();
}