using System.Text.Json;
using System.Text.Json.Serialization;

namespace JsonRpc;

public class JsonRpcMessage
{
    [JsonPropertyName("jsonrpc")] public string? JsonRpc { get; set; }

    [JsonPropertyName("id")] public long? Id { get; set; }

    [JsonPropertyName("result")] public object? Result { get; set; }

    [JsonPropertyName("error")] public JsonRpcError? Error { get; set; }

    [JsonPropertyName("method")] public string? Method { get; set; }

    [JsonPropertyName("params")] public JsonElement? Params { get; set; }
}

public class JsonRpcError
{
    [JsonPropertyName("code")] public long Code { get; set; }

    [JsonPropertyName("message")] public string Message { get; set; } = "";

    [JsonPropertyName("data")] public JsonElement? Data { get; set; }
}