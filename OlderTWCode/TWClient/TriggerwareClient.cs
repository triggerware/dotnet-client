using System.Net;
using Newtonsoft.Json;
using TWClients.JsonRpcMessages;

namespace TWClients;

public class TriggerwareClient : JsonRpcClient
{
    /// <summary>
    ///     methodCounter is used by Subscriptions and PolledQueries to ensure unique tags in notifications.
    /// </summary>
    private static ulong _methodCounter = 1;

    private static readonly PositionalParameterRequest<TwRuntimeMeasure> RuntimeRequest = new("runtime", 0, 0);

    private static readonly PositionalParameterRequest<VoidType> noopRequest = new("noop", 0, 0);

    private readonly HashSet<IPreparedQuery> _preparedQueries = [];

    private readonly IPAddress _twHost;

    private readonly int _twServerPort;

    public TriggerwareClient(string name, IPAddress twHost, int twServerPort)
        : base(twHost, twServerPort)
    {
        Name = name;
        _twHost = twHost;
        _twServerPort = twServerPort;
        EstablishTwCommunicationsAsync().Wait();
    }

    public bool TwCommsInitialized { get; private set; }

    /// <summary>
    ///     Determines the initial fetch size for a result set returned by requests that query
    ///     the TW server for data, unless overriden on the request. The size can an integer. It can also be null, which means
    ///     that
    ///     <em>all</em> results should be returned in the result set.
    /// </summary>
    public int? DefaultFetchSize { get; set; } = 10;

    /// <summary>
    ///     The default schema that will be used in connections created by this client.
    /// </summary>
    public string? DefaultSchema { get; set; } = null;

    /// <returns>
    ///     the network address used by this TriggerwareClient to connect to a server.
    /// </returns>
    public IPEndPoint ServerAddress => new(_twHost, _twServerPort);

    private static NamedParameterRequest<VoidType> SetSqlDefaultsRequest =>
        new("set-global-default", [], ["language", "sql-mode", "sql-namespace"]);

    public Dictionary<string, INotificationInducer> NotificationInducers { get; } = [];

    public bool AddPreparedQuery(IPreparedQuery pq)
    {
        return _preparedQueries.Add(pq);
    }

    public bool RemovePreparedQuery(IPreparedQuery pq)
    {
        return _preparedQueries.Remove(pq);
    }

    // [MethodImpl(MethodImplOptions.Synchronized)]
    private async Task EstablishTwCommunicationsAsync()
    {
        if (TwCommsInitialized) return;
        try
        {
            await SetSqlDefaultsAsync(null, "case-insensitive");
        }
        catch (JsonRpcRuntimeException e)
        {
            // change this to be a logger
            Logging.Log("failed to set sql case mode");
        }

        TwCommsInitialized = true;
    }

    private Task SetSqlDefaultsAsync(string? schema, string? mode)
    {
        var parameters = new Dictionary<string, object>();
        if (mode != null) parameters["sql-mode"] = mode;
        if (schema != null) parameters["sql-namespace"] = schema;
        return ExecuteAsync(SetSqlDefaultsRequest, parameters);
    }

    public static string? NextNotificationMethod(string prefix)
    {
        return prefix + Interlocked.Increment(ref _methodCounter);
    }

    public object RegisterNotificationInducer(string method, INotificationInducer inducer)
    {
        NotificationInducers.Add(method, inducer);
        return inducer;
    }

    public object UnregisterNotificationInducer(string method)
    {
        return NotificationInducers.Remove(method);
    }

    public TwRuntimeMeasure Runtime()
    {
        return Execute(RuntimeRequest);
    }

    public void Noop()
    {
        Execute(noopRequest);
    }

    public QueryStatement<TRow> CreateQuery<TRow>()
    {
        return new QueryStatement<TRow>(this);
    }
}

/// <summary>
///     TriggerwareClientException is the root class for exceptions that might be thrown by a TriggerwareClient
///     as a result of issuing a request to the server or handling a notification from the server.
///     A TriggerwareClientException is <em>not</em> a problem reported by the TW server.
/// </summary>
public class TriggerwareClientException : Exception
{
    public TriggerwareClientException(string message) : base(message)
    {
    }

    public TriggerwareClientException(string message, Exception innerException) : base(message, innerException)
    {
    }
}

[JsonConverter(typeof(TwRuntimeMeasureConverter))]
public class TwRuntimeMeasure
{
    public long RunTime { get; set; }

    public long GcTime { get; set; }

    public long Bytes { get; set; }
}

public class TwRuntimeMeasureConverter : JsonConverter<TwRuntimeMeasure>
{
    public override bool CanRead => true;
    public override bool CanWrite => true;

    public override void WriteJson(JsonWriter writer, TwRuntimeMeasure? value, JsonSerializer serializer)
    {
        if (value == null) return;
        var list = new List<long> { value.RunTime, value.GcTime, value.Bytes };
        serializer.Serialize(writer, list);
    }

    public override TwRuntimeMeasure ReadJson(JsonReader reader, Type objectType, TwRuntimeMeasure? existingValue,
        bool hasExistingValue, JsonSerializer serializer)
    {
        var list = serializer.Deserialize<List<long>>(reader);
        if (list == null || list.Count < 3)
            throw new JsonSerializationException("Expected a list with at least 3 elements.");

        return new TwRuntimeMeasure
        {
            RunTime = list[0],
            GcTime = list[1],
            Bytes = list[2]
        };
    }
}