namespace JsonRpc;

public class JsonRpcException(string message, long code) : Exception(message)
{
    public long Code => code;

    public JsonRpcError ToJsonRpcError()
    {
        return new JsonRpcError()
        {
            Code = Code,
            Message = Message
        };
    }
}

public class ParseErrorException() : JsonRpcException("Error parsing JSON-RPC message", -32700);

public class InvalidRequestException() : JsonRpcException("Invalid JSON-RPC message", -32600);

public class MethodNotFoundException(string method) : JsonRpcException("Method not found: " + method, -32601);

public class InvalidParamsException() : JsonRpcException("Invalid params", -32602);

public class InternalErrorException(string message) : JsonRpcException(message, -32603);

public class ServerErrorException(string message) : JsonRpcException(message, -32000);