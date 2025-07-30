using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;
using JsonRpc;
using Triggerware.Interfaces;

namespace Triggerware;

/// <summary>
///     A TriggerwareClient provides a connection to a triggerware server. A client contains methods for issuing a
///     few specific requests that are supported by any Triggerware server. Classes that extend TriggerwareClient
///     for specific applications will implement their own application-specific methods to make requests that are
///     idiosyncratic to a Triggerware server for that application.
///     <para></para>
///     A TriggerwareClient can also manage <see cref="Subscription{T}">Subscriptions</see>. By subscribing to certain
///     kinds of changes, the client arranges to be notified when these changes occur in the data accessible to the server.
/// </summary>
public class TriggerwareClient : JsonRpcClient
{
    private readonly object _rpcLock = new();

    private ulong _batchSubscriptionCounter = 0;
    private ulong _polledQueryCounter = 0;
    private ulong _subscriptionCounter = 0;

    /// <inheritdoc />
    /// <summary>
    ///     Creates a client connected to a triggerware server.
    /// </summary>
    public TriggerwareClient(IPAddress address, int port) : base(address, port)
    {
    }

    /// <summary>
    ///     The default fetch size for all queries to be executed.
    /// </summary>
    public uint? DefaultFetchSize { get; set; } = 10;

    /// <summary>
    ///     The default timeout that any query (which is allowed to timeout) will use.
    /// </summary>
    public float? DefaultTimeout { get; set; }

    /// <summary>
    ///     Executes a query on the connected server. The result is the same as if a <see cref="View{T}" /> had been
    ///     created and executed.
    /// </summary>
    /// <param name="query">
    ///     An object containing a query string and language/namespace restriction. Recommended to be either a
    ///     <see cref="SqlQuery" /> or a <see cref="FolQuery" />.
    /// </param>
    /// <param name="restriction">An optional <see cref="QueryRestriction" /> to control what is needed for this query. </param>
    /// <returns>A <see cref="ResultSet{T}" /> that stores the rows provided by the server.</returns>
    /// <exception cref="JsonRpcException">If the internal JsonRpc call fails.</exception>
    public ResultSet<T> ExecuteQuery<T>(IQuery query, QueryRestriction? restriction = null)
    {
        lock (_rpcLock)
        {
            var view = new View<T>(this, query, restriction);
            return view.Execute();
        }
    }

    /// <summary>
    ///     Checks whether a query string is valid for its specified language and namespace. will throw an
    ///     <see cref="InvalidQueryException" /> if invalid.
    /// </summary>
    /// <param name="query">
    ///     An object containing a query string and language/namespace restriction. Recommended to be either a
    ///     <see cref="SqlQuery" /> or a <see cref="FolQuery" />.
    /// </param>
    /// <exception cref="JsonRpcException">If an internal error causes the call to fail.</exception>
    /// <exception cref="InvalidQueryException">If the query is invalid.</exception>
    public void ValidateQuery(IQuery query)
    {
        lock (_rpcLock)
        {
            var parameters = new object[]
            {
                query.Query,
                query.Language,
                query.Schema
            };
            try
            {
                var _ = Call<string>("validate", parameters);
            }
            catch (InternalErrorException e)
            {
                throw;
            }
            catch (ServerErrorException e)
            {
                throw;
            }
            catch (JsonRpcException e)
            {
                throw new InvalidQueryException(e.Message);
            }
        }
    }

    /// <summary>
    ///     Noop request. Useful for performance testing.
    /// </summary>
    /// <exception cref="JsonRpcException">If the call fails.</exception>
    public void Noop()
    {
        lock (_rpcLock)
        {
            Call<object?>("noop", Array.Empty<object>());
        }
    }

    /// <summary>
    ///     Issue a request on this client's primary connection to obtain a time/space consumption measurement from the TW
    ///     server.
    /// </summary>
    /// <returns>The current TwRuntimeMeasure reported by the TW server.</returns>
    /// <exception cref="JsonRpcException">If the call fails.</exception>
    public TwRuntimeMeasure GetRuntimeMeasure()
    {
        lock (_rpcLock)
        {
            return Call<TwRuntimeMeasure>("runtime", Array.Empty<object>());
        }
    }

    /// <summary>
    /// Fetches a collection of connectors Triggerware supports, including their table names and signatures.
    /// </summary>
    /// <returns>Connector data, grouped by category</returns>
    public RelDataGroup[] GetRelData()
    {
        lock (_rpcLock)
        {
            return Call<RelDataGroup[]>("reldata2017", Array.Empty<object>());
        }
    }

    internal string RegisterPolledQuery()
    {
        return "poll" + _polledQueryCounter++;
    }
    
    internal string RegisterSubscription()
    {
        return "sub" + _subscriptionCounter++;
    }
    
    internal string RegisterBatchSubscription()
    {
        return "batch" + _batchSubscriptionCounter++;
    }
}


/// <summary>
///     TriggerwareClientException is the root class for exceptions that might be thrown by a TriggerwareClient
///     as a result of issuing a request to the server or handling a notification from the server.
///     A TriggerwareClientException is <em>not</em> a problem reported by the TW server.
/// </summary>
public class TriggerwareClientException(string message, Exception? inner = null) : Exception(message, inner)
{
}