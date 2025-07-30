using System.Collections;
using JsonRpc;
using Triggerware.Interfaces;
using Triggerware.JsonStructs;

namespace Triggerware;

/// <summary>
///     A result set is an abstraction of any result returned from the server when a query is executed. Result sets
///     contain a cache of rows initially returned from the server, and are responsible for fetching more from the server
///     when needed, internally calling the TW method 'next-resultset-batch'.
///     <para></para>
///     This result set may be treated as an enumerator - this will fetch rows until the result set is exhausted. If
///     only a set number of rows are needed instead, use <see cref="Pull" />. Alternatively, if a result set has just
///     been created and only the current cache is needed, use <see cref="CacheSnapshot" />.
/// </summary>
/// <typeparam name="T">The type of row this result set handles.</typeparam>
public class ResultSet<T>(TriggerwareClient client, ExecuteQueryResult<T> eqResult)
    : ITriggerwareObject, IResourceRestricted, IEnumerator<T>, IEnumerable<T>
{
    private readonly object _lock = new();
    private T[] _cache = eqResult.Batch.Rows;
    private int _cacheIdx;
    private T? _current;

    internal ResultSet(TriggerwareClient client, ExecuteQueryResult<T> eqResult, IResourceRestricted caller)
        : this(client, eqResult)
    {
        if (caller.RowLimit != null) RowLimit = caller.RowLimit;
        if (caller.Timeout != null) Timeout = caller.Timeout;
    }

    /// <summary>
    ///     Whether the result set has been exhausted.
    /// </summary>
    public bool Exhausted { get; private set; } = eqResult.Handle == null;

    /// <summary>
    ///     Whether the result set has been disposed.
    /// </summary>
    public bool IsDisposed { get; private set; }

    public IEnumerator<T> GetEnumerator()
    {
        return this;
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return this;
    }

    /// <summary>
    ///     Disposes of this result-set. Will call 'close-resultset' on the server.
    /// </summary>
    public void Dispose()
    {
        IsDisposed = true;
        lock (_lock)
        {
            if (Handle != null)
                Client.Call<object?>("close-resultset", [Handle]);

            GC.SuppressFinalize(this);
        }
    }

    /// <summary>
    ///     Steps to the next row in the result set. Will exhaust this result set only if there are no more rows
    ///     in both the cache or ready in the server.
    /// </summary>
    /// <returns>false when there are no more rows to be found, true otherwise.</returns>
    /// <exception cref="ResultSetException">When the Triggerware server fails to execute more tuples.</exception>
    public bool MoveNext()
    {
        lock (_lock)
        {
            if (_cacheIdx >= _cache.Length)
            {
                _current = default;
                return false;
            }

            _current = _cache[_cacheIdx];
            _cacheIdx++;

            if (_cacheIdx >= _cache.Length || Exhausted) return true;


            ExecuteQueryResult<T>? result;
            try
            {
                result = Client.Call<ExecuteQueryResult<T>>("next-resultset-batch", [Handle, RowLimit, Timeout]);
            }
            catch (JsonRpcException e)
            {
                throw new ResultSetException("internal error", e);
            }

            _cache = result.Batch.Rows;
            _cacheIdx = 0;
            Exhausted = result.Batch.Exhausted;

            return true;
        }
    }

    /// <summary>
    ///     Because result sets cannot be reset, this method will ALWAYS throw a <see cref="ResultSetException" />.
    /// </summary>
    /// <exception cref="ResultSetException">Will always throw.</exception>
    public void Reset()
    {
        throw new ResultSetException(
            "Result Sets cannot be reset. To start again, you must create another one with the same query used" +
            "to create this one.", null);
    }

    /// <summary>
    ///     The current row in the result set.
    /// </summary>
    public T Current => _current!;

    object IEnumerator.Current => Current!;

    public uint? RowLimit { get; set; } = client.DefaultFetchSize;
    public double? Timeout { get; set; } = client.DefaultTimeout;

    public TriggerwareClient Client { get; } = client;
    public long? Handle { get; } = eqResult.Handle;

    /// <summary>
    ///     Fetches the next n rows from the result set, or as many as possible if there are less than n rows left.
    /// </summary>
    public T[] Pull(int n)
    {
        var result = new T[n];
        for (var i = 0; i < n; i++)
        {
            if (!MoveNext()) break;
            result[i] = Current;
        }

        return result.Where(x => x != null).ToArray();
    }

    /// <summary>
    ///     Returns a copy of the current cache.
    /// </summary>
    public T[] CacheSnapshot()
    {
        return _cache.Select(x => x).ToArray();
    }
}

public class ResultSetException(string message, Exception? inner) : TriggerwareClientException(message, inner);