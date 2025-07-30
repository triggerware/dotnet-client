using System.Runtime.CompilerServices;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using TWClients.JsonRpcMessages;

namespace TWClients;

/// <summary>
///     A statement that can be used to issue query requests on a connection.
///     The statement may be executed multiple times on a connection.
///     A QueryStatement may also be executed using <see cref="ExecuteQueryWithPushResults" /> to have
///     the rows 'streamed' back to the client on the connection.
/// </summary>
/// <param name="client"></param>
/// <typeparam name="TRow"></typeparam>
public class QueryStatement<TRow>(TriggerwareClient client) : IStatement

{
    public bool Closed { get; } = false;
    public int? FetchSize { get; set; }
    public TWResultSet<TRow>? ResultSet { get; private set; }
    public JsonRpcClient Client => client;

    public void Dispose()
    {
        Client.Dispose();
        ResultSet.Dispose();
    }

    [MethodImpl(MethodImplOptions.Synchronized)]
    public int? SetFetchSize(int? fetchSize)
    {
        var old = FetchSize;
        FetchSize = fetchSize;
        return old;
    }

    /// <summary>
    ///     Execute an ad-hoc SQL query, returning a ResultSet.
    /// </summary>
    /// <param name="query">the query string</param>
    /// <returns>A TWResultSet with the first patch of results from executing this query.</returns>
    public async Task<TWResultSet<TRow>> ExecuteQueryWithDefaultsAsync(string query)
    {
        var qrl = FetchSize == null ? null : new QueryResourceLimits(null, FetchSize);
        var parameters = new Dictionary<string, object>
        {
            { "query", query }
        };
        if (qrl != null)
        {
            parameters["limit"] = qrl.RowCountLimit;
            parameters["timelimit"] = qrl.Timeout;
        }

        var x = await client.Rpc.InvokeWithParameterObjectAsync<JObject>("execute-query",
            new Dictionary<string, object> { { "query", query } });
        Console.WriteLine(x.ToString());

        // var eqResult =
        //     await client.Rpc.InvokeWithParameterObjectAsync<ExecuteQueryResult<TRow>>("execute-query", parameters);
        // var rs = new TWResultSet<TRow>(this, FetchSize, eqResult, null);
        // ResultSet = rs;

        // return rs;
        return null;
    }

    /// <summary>
    ///     Execute an ad-hoc SQL query, returning a ResultSet.
    /// </summary>
    /// <param name="query">the query string</param>
    /// <param name="schema">either Language.Fol or Language.Sql.</param>
    /// <returns>A TWResultSet with the first patch of results from executing this query.</returns>
    public Task<TWResultSet<TRow>> ExecuteQueryAsync(string query, string schema)
    {
        var qrl = FetchSize == null ? null : new QueryResourceLimits(null, FetchSize);
        return ExecuteQueryAsync(query, Language.Sql, schema, qrl);
    }

    /// <summary>
    ///     Execute an ad-hoc query, returning a ResultSet.
    /// </summary>
    /// <param name="query">the query string</param>
    /// <param name="language">the schema (sql) or package (fol) name to use for interpreting the query string.</param>
    /// <param name="schema">either Language.Fol or Language.Sql.</param>
    /// <returns>A TWResultSet with the first patch of results from executing this query.</returns>
    public Task<TWResultSet<TRow>> ExecuteQueryAsync(string query, string language, string schema)
    {
        var qrl = FetchSize == null ? null : new QueryResourceLimits(null, FetchSize);
        return ExecuteQueryAsync(query, language, schema, qrl);
    }

    /// <summary>
    ///     Execute an ad-hoc query, returning a ResultSet.
    /// </summary>
    /// <param name="query">the query string</param>
    /// <param name="language">the schema (sql) or package (fol) name to use for interpreting the query string.</param>
    /// <param name="schema">either Language.Fol or Language.Sql.</param>
    /// <param name="qrl">resource limits on executing the query - null is no limits.</param>
    /// <returns>A TWResultSet with the first patch of results from executing this query.</returns>
    public async Task<TWResultSet<TRow>> ExecuteQueryAsync(string query, string language, string schema,
        QueryResourceLimits? qrl)
    {
        var parameters = CommonParams(query, language, schema);
        if (qrl != null)
        {
            parameters["limit"] = qrl.RowCountLimit;
            parameters["timelimit"] = qrl.Timeout;
        }

        var eqResult =
            await client.Rpc.InvokeWithParameterObjectAsync<ExecuteQueryResult<TRow>>("execute-query", parameters);
        var rs = new TWResultSet<TRow>(this, FetchSize, eqResult, null);
        ResultSet = rs;

        return rs;
    }

    public void ExecuteQueryWithPushResults(string query, string queryLanguage, string schema,
        PushResultController<TRow> controller)
    {
        CommonCheck();
    }

    private void CommonCheck()
    {
        if (Closed) throw new TriggerwareClientException("attempt to execute a closed QueryStatement");
        if (ResultSet != null)
        {
            ResultSet.Dispose();
            ResultSet = null;
        }
    }

    private Dictionary<string, object> CommonParams(string query, string language, string schema)
    {
        return new Dictionary<string, object>
        {
            { "query", query },
            { "language", language },
            { "schema", schema },
            { "check-update", false }
        };
    }
}

[method: JsonConstructor]
public class Batch<TRow>(long count, List<TRow> tuples, bool exhausted)
{
    public long Count => count;
    [JsonProperty("tuples")] public List<TRow>? Rows => tuples;
    public bool Exhausted => exhausted;
}

[method: JsonConstructor]
public class ExecuteQueryResult<TRow>(int? handle, Batch<TRow> batch, SignatureElement[] signature)
{
    public int? Handle => handle;
    public Batch<TRow> Batch => batch;
    public SignatureElement[] Signature => signature;
}

/// <summary>
///     QueryResourceLimits holds resource limits that the TW server will obey when computing an answer to a query.
///     This object can be used with any request that returns a TWResultSet.
///     Currently there are two kinds of limit:
///     <ul>
///         <li>
///             a time limit, measured in seconds.  A null value means no timeout will be used.
///             When a timeout is exceeded on the server, the result set returned may contain fewer rows than requested.
///         </li>
///         <li>
///             a rowCountLimit, which is a limit on the <em>total</em> number of rows that will be returned in batches
///             before the TW server will regard the results as exhausted.  The default is NULL, meaning no limit is
///             imposed
///             by the QueryResourceLimits.  Even if a query executed by executeQuery has its own limit clause, the
///             rowCountLimit
///             can further limit the number of 'row' values that will be returned. It cannot <em>extend</em> that limit,
///             however.
///         </li>
///     </ul>
/// </summary>
/// <param name="timeout">optional timeout value</param>
/// <param name="rowCountLimit">a limit on the number of row values to be returned</param>
public class QueryResourceLimits(double? timeout = null, int? rowCountLimit = null)
{
    public int? RowCountLimit
    {
        get => rowCountLimit;
        set
        {
            if (value is < 1)
                throw new ArgumentException("RowCountLimit must be null or positive");

            rowCountLimit = value;
        }
    }

    public double? Timeout
    {
        get => timeout;
        set
        {
            if (value is <= 0.0)
                throw new ArgumentException("Timeout must be null or positive");

            timeout = value;
        }
    }
}