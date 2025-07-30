namespace TWClients.JsonRpcMessages;

/// <summary>
///     JsonRpcException is the root class for exceptions that might be thrown by any issue to do with the JsonRpcClient.
/// </summary>
public class JsonRpcException: Exception, IJsonRpcProblem
{
    private static readonly int parseErrorCode = -32700;
    private static readonly int invalidRequestCode = -32600;
    private static readonly int methodNotFoundCode = -32601;
    private static readonly int invalidParamsCode = -32602;
    private static readonly int internalErrorCode = -32603;

    public JsonRpcException(string explanation, Exception? e = null) : base(explanation, e)
    {
    }

    public JsonRpcException(string exception, int code) : base(exception)
    {
        Code = code;
    }

    public int Code { get; }
}
/// <summary>
///     JRPCAgentException is the root class for exceptions that might be thrown by a JRPCAgent
///     as a result of issuing a request or notification to its partner or handling a notification from its partner.
/// </summary>
public abstract class JsonRpcAgentException(string explanation, Exception? e = null) : JsonRpcException(explanation, e)
{
}

