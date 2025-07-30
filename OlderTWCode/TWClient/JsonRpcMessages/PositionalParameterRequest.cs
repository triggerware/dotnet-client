using StreamJsonRpc;

namespace TWClients.JsonRpcMessages;

public class PositionalParameterRequest<TResult>(string method, int minParameterCount, int maxParameterCount)
    : OutboundRequest<TResult>(method)
{
    public int MaxParameterCount => maxParameterCount;
    public int MinParameterCount => minParameterCount;

    public void Validate(params object?[] parameters)
    {
        var n = parameters.Length;
        if (n < minParameterCount)
            throw new JsonRpcRuntimeException.ActualParameterException("too few parameters");
        if (n > maxParameterCount)
            throw new JsonRpcRuntimeException.ActualParameterException("too many parameters");
    }
}



