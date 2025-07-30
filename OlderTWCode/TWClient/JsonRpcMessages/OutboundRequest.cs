using StreamJsonRpc;

namespace TWClients.JsonRpcMessages;

public abstract class OutboundRequest<TResult>(string methodName)
{
    public string MethodName => methodName;
    public RequestId RequestId { get; set; } = new(-1);
}