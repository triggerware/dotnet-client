using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace JsonRpc;

/// <summary>
///     The JsonRpcClient class is a two-way connection that implements the JSON-RPC protocol in its most basic form. It
///     interprets every message it receives as a part of a json message. This means no leading metadata, no
///     message-separation characters.
/// </summary>
public class JsonRpcClient : IDisposable
{
    private readonly object _disposedLock = new();
    private readonly Dictionary<long, JsonRpcMessage> _incoming = [];
    private readonly Dictionary<string, Delegate> _methods = [];
    private readonly List<JsonRpcMessage> _outgoing = [];
    private readonly Thread _readThread;
    private readonly Socket _socket;
    private readonly Thread _writeThread;
    private byte[] _buffer = [];
    private bool _disposed;
    private long _id;
    private bool _listening;

    /// <summary>
    /// Initializes a new instance of the <see cref="JsonRpcClient"/> class.
    /// </summary>
    /// <param name="address">The IP address of the server.</param>
    /// <param name="port">The port number to connect to.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="address"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when <paramref name="port"/> is not between 1 and 65535.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the client fails to connect to the specified endpoint.
    /// </exception>
    public JsonRpcClient(IPAddress address, int port)
    {
        Address = address;
        Port = port;

        _readThread = new Thread(Read);
        _readThread.IsBackground = true;

        _writeThread = new Thread(Write);
        _writeThread.IsBackground = true;

        var endpoint = new IPEndPoint(address, port);
        _socket = new Socket(endpoint.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
        _socket.Connect(endpoint);
    }

    public IPAddress Address { get; }
    public int Port { get; }

    private long Id
    {
        get
        {
            var id = _id;
            _id += 1;
            return id;
        }
    }

    public void Dispose()
    {
        lock (_disposedLock)
        {
            _disposed = true;
            _socket.Dispose();
            GC.SuppressFinalize(this);
        }
    }

    /// <summary>
    ///     Starts the client, allowing it to send and receive messages. This will spawn two additional threads, one for
    ///     reading
    ///     from the stream and one for writing to it.
    /// </summary>
    public void Start()
    {
        _listening = true;
        _readThread.Start();
        _writeThread.Start();
    }

    public bool IsDisposed()
    {
        lock (_disposedLock)
        {
            return _disposed;
        }
    }

    /// <summary>
    ///     Sends a notification over json-rpc.
    /// </summary>
    /// <param name="method">The name of the method.</param>
    /// <exception cref="JsonRpcException">If the notification is sent while the connection is not active.</exception>
    public void Notify(string method)
    {
        NotifyPrivate(method, new object[] { });
    }

    /// <summary>
    ///     Sends a notification over json-rpc.
    /// </summary>
    /// <param name="method">The name of the method.</param>
    /// <param name="namedParams">The parameters of the method call.</param>
    /// <exception cref="JsonRpcException">If the notification is sent while the connection is not active.</exception>
    public void Notify(string method, Dictionary<string, object> namedParams)
    {
        NotifyPrivate(method, namedParams);
    }

    /// <summary>
    ///     Sends a notification over json-rpc.
    /// </summary>
    /// <param name="method">The name of the method.</param>
    /// <param name="positionalParams">The parameters of the method call.</param>
    /// <exception cref="JsonRpcException">If the notification is sent while the connection is not active.</exception>
    public void Notify(string method, object?[] positionalParams)
    {
        NotifyPrivate(method, positionalParams);
    }

    private void NotifyPrivate(string method, object? parameters)
    {
        if (!_listening)
            throw new JsonRpcException("Call 'Start' before notifying a connection!", 0);
        var serializedParams = JsonSerializer.SerializeToUtf8Bytes(parameters);
        var message = new JsonRpcMessage
        {
            JsonRpc = "2.0",
            Method = method,
            Params = JsonSerializer.Deserialize<JsonElement>(serializedParams)
        };
        lock (_outgoing)
        {
            _outgoing.Add(message);
        }
    }

    /// <summary>
    ///     Calls a method over json-rpc.
    /// </summary>
    /// <param name="method">The name of the method.</param>
    /// <typeparam name="T">The expected return type to serialize.</typeparam>
    /// <returns>The result of the method call.</returns>
    /// <exception cref="JsonRpcException">If the JSON-RPC call fails with an error response.</exception>
    /// <exception cref="InternalErrorException">If deserialization of the response fails.</exception>
    /// <exception cref="ServerErrorException">If the connection to the server is lost.</exception>
    public T Call<T>(string method)
    {
        return CallPrivate<T>(method, Array.Empty<object>());
    }

    /// <summary>
    ///     Calls a method over json-rpc.
    /// </summary>
    /// <param name="method">The name of the method.</param>
    /// <param name="positionalParams">The parameters of the method call.</param>
    /// <typeparam name="T">The expected return type to serialize.</typeparam>
    /// <returns>The result of the method call.</returns>
    /// <exception cref="JsonRpcException">If the JSON-RPC call fails with an error response.</exception>
    /// <exception cref="InternalErrorException">If deserialization of the response fails.</exception>
    /// <exception cref="ServerErrorException">If the connection to the server is lost.</exception>
    /// /
    public T Call<T>(string method, object?[] positionalParams)
    {
        return CallPrivate<T>(method, positionalParams);
    }

    /// <summary>
    ///     Calls a method over json-rpc.
    /// </summary>
    /// <param name="method">The name of the method.</param>
    /// <param name="namedParams">The parameters of the method call.</param>
    /// <typeparam name="T">The expected return type to serialize.</typeparam>
    /// <returns>The result of the method call.</returns>
    /// <exception cref="JsonRpcException">If the JSON-RPC call fails with an error response.</exception>
    /// <exception cref="InternalErrorException">If deserialization of the response fails.</exception>
    /// <exception cref"ServerErrorException">If the connection to the server is lost.</exception>
    /// /
    public T Call<T>(string method, Dictionary<string, object> namedParams)
    {
        return CallPrivate<T>(method, namedParams);
    }

    private T CallPrivate<T>(string method, object? parameters)
    {
        if (!_listening)
            throw new JsonRpcException("Call 'Start' before invoking a method on a connection!", 0);
        
        var id = Id;
        var serializedParams = JsonSerializer.SerializeToUtf8Bytes(parameters);
        var message = new JsonRpcMessage
        {
            JsonRpc = "2.0",
            Id = id,
            Method = method,
            Params = JsonSerializer.Deserialize<JsonElement>(serializedParams)
        };
        lock (_outgoing)
        {
            _outgoing.Add(message);
        }

        JsonRpcMessage? response;

        lock (_incoming)
        {
            while (!_incoming.TryGetValue(id, out response))
            {
                Monitor.Wait(_incoming);
                if (IsDisposed())
                    throw new ServerErrorException("Connection to server lost.");
            }
            _incoming.Remove(id);
        }

        if (response.Error != null)
            throw new JsonRpcException(response.Error.Message, response.Error.Code);
        
        string rawResult;
        if (response.Result is JsonElement element)
            rawResult = element.GetRawText();
        else
            rawResult = JsonSerializer.Serialize(response.Result);
        
        try
        {
            return JsonSerializer.Deserialize<T>(rawResult)!;
        }
        catch (Exception e)
        {
            throw new InternalErrorException(e.Message);
        }
    }

    /// <summary>
    ///     Adds a method to this connection. When notifications or calls are sent to this object, the added delegate will
    ///     be called.
    /// </summary>
    /// <param name="methodName">The name of the added method.</param>
    /// <param name="method">
    ///     The callable object whose result will be serialized and sent back to the caller (unless it's a notification).
    /// </param>
    /// <returns></returns>
    public bool AddMethod(string methodName, Delegate method)
    {
        lock (_methods)
        {
            return _methods.TryAdd(methodName, method);
        }
    }

    /// <summary>
    ///     Removes a previously added method.
    /// </summary>
    /// <param name="methodName">The name of the method to remove.</param>
    /// <returns>True is the method was indeed in the methods collection, false otherwise.</returns>
    public bool RemoveMethod(string methodName)
    {
        lock (_methods)
        {
            return _methods.Remove(methodName);
        }
    }

    private void Read()
    {
        while (!IsDisposed())
        {
            var bytes = new byte[2048];
            var read = _socket.Receive(bytes);
            _buffer = _buffer.Concat(bytes[..read]).ToArray();
            try
            {
                var reader = new Utf8JsonReader(_buffer);
                var message = JsonSerializer.Deserialize<JsonRpcMessage>(
                    ref reader,
                    JsonSerializerOptions.Default
                )!;
                var pos = (int)reader.BytesConsumed;
                _buffer = _buffer[pos..];

                if (message.Method != null)
                    HandleIncomingRequest(message);
                else
                    HandleIncomingResponse(message);
            }
            catch (Exception e)
            {
                // Console.WriteLine("error ", e.Message);
                // ignore
                // probably log this somehow in the future
            }

            Thread.Yield();
        }
    }

    private void Write()
    {
        while (!IsDisposed())
        {
            lock (_outgoing)
            {
                foreach (var message in _outgoing)
                {
                    var bytes = JsonSerializer.SerializeToUtf8Bytes(message, new JsonSerializerOptions
                    {
                        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
                    });
                    Console.WriteLine("SENDING: " + Encoding.UTF8.GetString(bytes));
                    _socket.Send(bytes);
                }

                _outgoing.Clear();
            }

            Thread.Yield();
        }
    }

    private void HandleIncomingResponse(JsonRpcMessage message)
    {
        if (message.Id == null)
            return;
        lock (_incoming)
        {
            _incoming[message.Id!.Value] = message;
            Monitor.PulseAll(_incoming);
        }
    }

    private void HandleIncomingRequest(JsonRpcMessage message)
    {
        if (message.Id == null)
            try
            {
                InvokeMethod(message.Method!, message.Params!.Value);
                return;
            }
            catch (Exception e)
            {
                return;
            }

        JsonRpcMessage response;
        if (message.Method == null || !message.Params.HasValue)
            response = new JsonRpcMessage
            {
                JsonRpc = "2.0",
                Id = message.Id,
                Error = new InvalidRequestException().ToJsonRpcError()
            };
        else
            try
            {
                response = new JsonRpcMessage
                {
                    JsonRpc = "2.0",
                    Id = message.Id,
                    Result = InvokeMethod(message.Method, message.Params.Value)
                };
            }
            catch (JsonRpcException e)
            {
                response = new JsonRpcMessage
                {
                    JsonRpc = "2.0",
                    Id = message.Id,
                    Error = e.ToJsonRpcError()
                };
            }

        lock (_outgoing)
        {
            _outgoing.Add(response);
        }
    }

    private object? InvokeMethod(string methodName, JsonElement paramsElement)
    {
        lock (_methods)
        {
            if (!_methods.TryGetValue(methodName, out var method))
                throw new MethodNotFoundException(methodName);

            try
            {
                var paramInfo = method.GetMethodInfo().GetParameters();
                var rawParameters = paramsElement.ValueKind switch
                {
                    JsonValueKind.Object => paramInfo.Select(x => paramsElement.GetProperty(x.Name!).GetRawText()),
                    JsonValueKind.Array => paramsElement.EnumerateArray().Select(x => x.GetRawText()),
                    _ => [paramsElement.GetRawText()]
                };
                var parameters = paramInfo.Select(x => x.ParameterType)
                    .Zip(rawParameters, (type, param) => JsonSerializer.Deserialize(param, type))
                    .ToArray();
                return method.DynamicInvoke(parameters);
            }
            catch (MemberAccessException)
            {
                throw new MethodNotFoundException(methodName);
            }
            catch (Exception e) when (
                e is KeyNotFoundException
                    or TargetParameterCountException
                    or ArgumentException
                    or TargetInvocationException
                    or MemberAccessException)
            {
                throw new InvalidParamsException();
            }
            catch (JsonException)
            {
                throw new ParseErrorException();
            }
            catch (Exception e)
            {
                throw new InternalErrorException(e.Message);
            }
        }
    }
}