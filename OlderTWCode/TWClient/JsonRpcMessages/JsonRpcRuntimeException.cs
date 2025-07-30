namespace TWClients.JsonRpcMessages;

/// <summary>
///     A JsonRpcRuntimeException may be thrown due to problems detected when transmitting a request or processing a
///     response.
/// </summary>
public abstract class JsonRpcRuntimeException(string explanation, int code, Exception? e = null)
    : JsonRpcException(explanation, e)
{
    private const int DeserializationErrorCode = 50;
    private const int SerializationErrorCode = 51;
    private const int CommunicationsErrorCode = 52;
    private const int ParameterErrorCode = 53;
    private const int MethodNotFoundCode = -32601;

    public int Code => code;

    /// <summary>
    ///     A DeserializationFailure exception is thrown if a request or response cannot be deserialized.
    /// </summary>
    public class DeserializationFailure(string msg, Exception? e = null)
        : JsonRpcRuntimeException(msg, DeserializationErrorCode, e)
    {
    }

    /// <summary>
    ///     An UnknownMethodException is thrown if a request is made for a method that is not recognized.
    /// </summary>
    public class UnknownMethodException(string explanation)
        : JsonRpcRuntimeException("no registered handler for method [" + explanation + "]", MethodNotFoundCode);

    /// <summary>
    ///     A SerializationFailure exception is thrown if a request or response cannot be serialized.
    /// </summary>
    public class SerializationFailure(string explanation, Exception? e)
        : JsonRpcRuntimeException(explanation, SerializationErrorCode, e);

    /// <summary>
    ///     A CommunicationsFailure exception is thrown if the communication channel between client and server breaks.
    /// </summary>
    public class CommunicationsFailure(string explanation, Exception? e)
        : JsonRpcRuntimeException(explanation, CommunicationsErrorCode, e);

    /// <summary>
    ///     Parameters for requests are understood either 'positionally' or 'by name', depending on the request.
    ///     In either case, the supplied parameters may be determined to be invalid.  This exception is thrown
    ///     for parameter errors detected by this library before the request is sent to the server. When this
    ///     exception is thrown the request is not actually issued.
    /// </summary>
    public class ActualParameterException(string explanation)
        : JsonRpcRuntimeException(explanation, ParameterErrorCode);
}