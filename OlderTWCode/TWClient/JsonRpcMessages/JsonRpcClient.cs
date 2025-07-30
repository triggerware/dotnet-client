using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Text;
using Microsoft.VisualStudio.Threading;
using StreamJsonRpc;

namespace TWClients.JsonRpcMessages;

/// <summary>
///     A JsonRpcClient represents a client that communicates with a server via TCP using the Json RPC
///     protocol.
/// </summary>
public abstract class JsonRpcClient : IDisposable
{
    private const string DefaultName = "anonymouse client";

    private int _requestCounter = 1;
   
    private object _rpcLock = new();

    protected JsonRpcClient(IPAddress networkAddress, int port)
    {
        NetworkAddress = networkAddress;
        Port = port;
        Rpc = CreateJsonRpc(networkAddress, port); 
        Rpc.StartListening();
        // PrimaryJsonRpcConnection = new JsonRpcConnection(this, networkAddress, port);
        // PrimaryJsonRpcConnection.Rpc.StartListening();
    }

    private static JsonRpc CreateJsonRpc(IPAddress ipAddress, int port)
    {
        var ipEndPoint = new IPEndPoint(ipAddress, port);
        var socket = new Socket(ipEndPoint.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
        socket.Connect(ipEndPoint);
        var stream = new NetworkStream(socket, false);
        var messageFormatter = new JsonMessageFormatter(new UTF8Encoding(false));
        var messageHandler = new NewLineDelimitedMessageHandler(stream, stream, messageFormatter);
        return new JsonRpc(messageHandler);
    }

    // public JsonRpcConnection PrimaryJsonRpcConnection { get; }

    public JsonRpc Rpc { get; set; }
   
    /// <summary>
    ///     The network address of the agents partner, if  on a network connection.
    /// </summary>
    public IPAddress NetworkAddress { get; }

    public int Port { get; }
    public string Name { get; set; } = DefaultName;

    protected int NextRequestId => Interlocked.Increment(ref _requestCounter);

    public bool ShuttingDown { get; protected set; }

    public void Dispose()
    {
        Rpc.Dispose();
        GC.SuppressFinalize(this);
    }

    public Task<TReturn> ExecuteAsync<TReturn>(NamedParameterRequest<TReturn> request,
        Dictionary<string, object?> namedParameters)
    {
        request.Validate(namedParameters);
        return Rpc.InvokeWithParameterObjectAsync<TReturn>(request.MethodName, namedParameters);
    }
   
    public Task<TReturn> ExecuteAsync<TReturn>(PositionalParameterRequest<TReturn> request,
        params object?[] parameters)
    {
        request.Validate(parameters);
        return Rpc.InvokeAsync<TReturn>(request.MethodName, parameters);
    }
   
    public TReturn Execute<TReturn>(NamedParameterRequest<TReturn> request,
        Dictionary<string, object?> namedParameters)
    {
        lock (_rpcLock)
        {
            return ExecuteAsync(request, namedParameters).Result;
        }
    }
   
    public TReturn Execute<TReturn>(PositionalParameterRequest<TReturn> request,
        params object?[] parameters)
    {
        lock (_rpcLock)
        {
            return ExecuteAsync(request, parameters).Result;
        }
    }
  
    public Task NotifyAsync<TReturn>(NamedParameterRequest<TReturn> request,
        Dictionary<string, object?> namedParameters)
    {
        request.Validate(namedParameters);
        return Rpc.NotifyWithParameterObjectAsync(request.MethodName, namedParameters);
    }

    public Task NotifyAsync<TReturn>(PositionalParameterRequest<TReturn> request, params object?[] parameters)
    {
        request.Validate(parameters);
        return Rpc.NotifyAsync(request.MethodName, parameters);
    }

    public void Notify<TReturn>(NamedParameterRequest<TReturn> request,
        Dictionary<string, object?> namedParameters)
    {
        lock (_rpcLock)
        {
            NotifyAsync(request, namedParameters).Wait();
        }
    }

    public void Notify<TReturn>(PositionalParameterRequest<TReturn> request, params object?[] parameters)
    {
        lock (_rpcLock)
        {
            NotifyAsync(request, parameters).Wait();
        }
    }
}