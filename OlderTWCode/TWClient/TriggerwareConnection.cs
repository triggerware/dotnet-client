using StreamJsonRpc;

namespace TWClients;

public class TriggerwareConnection : JsonRpc
{
    public TriggerwareConnection(TriggerwareClient twClient, Stream stream) : base(stream)
    {
        TwClient = twClient;
    }

    public TriggerwareConnection(TriggerwareClient twClient, Stream? sendingStream, Stream? receivingStream,
        object? target = null) : base(sendingStream, receivingStream, target)
    {
        TwClient = twClient;
    }

    public TriggerwareConnection(TriggerwareClient twClient, IJsonRpcMessageHandler messageHandler, object? target) :
        base(messageHandler, target)
    {
        TwClient = twClient;
    }

    public TriggerwareConnection(TriggerwareClient twClient, IJsonRpcMessageHandler messageHandler) : base(
        messageHandler)
    {
        TwClient = twClient;
    }

    public string? DefaultSchema { get; set; } = null;
    public HashSet<IPreparedQuery> PreparedQueries { get; } = [];
    public HashSet<View> Views { get; } = [];

    public TriggerwareClient TwClient { get; }
}