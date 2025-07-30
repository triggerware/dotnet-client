using System.Runtime.CompilerServices;
using Microsoft.VisualStudio.Threading;
using TWClients.JsonRpcMessages;

namespace TWClients;

/// <summary>
///     A TWResultSet represents the set of rows that are the result of a query. It provides
///     forward-only iteration over that set via Next and Get.
///     <para></para>
///     At any time a TWResultSet holds a cache of rows recieved
///     from the server but not yet delivered to the iteration. An attempt to iterate to the next
///     row when the cache is empty will cause the TWResultSet to request more rows from the server.
///     <para></para>
///     Each row of a result set is an object[]. Each row will have the same length, and will
///     have elements belonging to the types of the TWResultSet's signature. The length and type
///     restrictions are <em>ensured</em> correct when the rows are produced (by deserializing them from results of TW
///     server requests.
///     <para></para>
///     This implementation of TWResultSet is <em>not</em> thread-safe. A program that accesses a TWResultSet from multiple
///     threads
///     is responsible for serializing access to it.
/// </summary>
/// <typeparam name="TRow"></typeparam>
public class TWResultSet<TRow> : IDisposable
{
    private static readonly TWResultSetException closedResultSetException = new("operation on a closed result set");

    public static readonly PositionalParameterRequest<VoidType> CloseResultSetRequest = new("close-resultset", 1, 1);

    private static readonly PositionalParameterRequest<Batch<TRow>>
        NextBatchRequest = new("next-resultset-batch", 2, 3);

    public SignatureElement[]? Signature { get; private set; }

    public TWResultSet(IStatement statement, int? fetchSize, ExecuteQueryResult<TRow> eqResult,
        PreparedQuery<TRow>? pq)
    {
        Statement = statement;
        Client = statement.Client;
        FetchSize = fetchSize;
        Handle = eqResult.Handle;
        Exhausted = Handle == null;

        var rows = eqResult.Batch.Rows;
        if (rows != null)
            foreach (var row in rows)
                Cache.Enqueue(row);

        //        if (rows != null) Cache.AddRange(rows.Select(x => x));
        if (pq == null)
        {
            Signature = eqResult.Signature;
            ColumnNames = Signature?.Select(s => s.Name).ToArray();
        }
        else
        {
            Signature = null;
            ColumnNames = pq.InputSignatureNames;
        }
    }

    // the names of the columns for the columns of this result set.
    public string?[]? ColumnNames { get; }

    /// <summary>
    ///     Obtain the signature of this TWResultSet. Each row delivered by Get is an object[] whose length is the signature's
    ///     length and whose i'th element is an instance of the class that is the ith element of the signature.
    ///     <para></para>
    ///     Applications will
    ///     rarely need this property because the programmer will know at coding time what the row element types are, and will
    ///     simply
    ///     case the element values to the promised type when consuming them.
    /// </summary>
    public Type RowSignature => typeof(TRow);

    /// <summary>
    ///     true if the TWResultSet has been closed. A TWResultSet can be explicitly closed by the Close method, or indirectly
    ///     closed if the statemenet which produced it is closed.
    /// </summary>
    public bool Closed { get; private set; }

    /// <summary>
    ///     true if this TWResultSet can obtain no more rows from the TW server. Otherwise false.
    /// </summary>
    public bool Exhausted { get; private set; } = false;

    private Queue<TRow> Cache { get; } = [];

    /// <summary>
    ///     the statement used to create this result set.
    /// </summary>
    public IStatement Statement { get; }

    /// <summary>
    ///     true if Next has not yet been invoked for this TWResultSet, otherwise false.
    /// </summary>
    public bool IsBeforeFirst { get; private set; } = true;

    /// <summary>
    ///     true if Next has been invoked for this TWResultSet and returned false, indicating
    ///     that no more rows are available. Otherwise false.
    /// </summary>
    public bool IsAfterLast { get; private set; }

    public TRow? Current { get; private set; }

    public int RowNumber { get; private set; }

    /// <summary>
    ///     The fetch size may be changed if Next needs to retrieve addtional rows from the TW server.
    /// </summary>
    public int? FetchSize { get; set; }

    /// <summary>
    ///     The timeout may be changed if Next needs to retrieve additional rows from the TW server,
    ///     measured in seconds.
    /// </summary>
    public double? Timeout { get; } = null;

    public int? Handle { get; }

    public Batch<TRow>? Batch { get; }
    public JsonRpcClient Client { get; }

    public void Dispose()
    {
        if (Closed) return;
        RowNumber = 0;
        if (Exhausted)
        {
            Closed = true;
            return;
        }

        Exhausted = true;
        Closed = true;
        OnDispose();
    }

    public List<TRow> CacheSnapshot(bool clearCache)
    {
        var snap = Cache.Select(x => x).ToList();
        if (clearCache) Cache.Clear();
        return snap;
    }

    [MethodImpl(MethodImplOptions.Synchronized)]
    public bool Next()
    {
        if (Closed) throw closedResultSetException;
        if (Cache.Count != 0)
        {
            IsBeforeFirst = false;
            Current = Cache.Dequeue();
            RowNumber++;
            return true;
        }

        if (Exhausted)
        {
            IsAfterLast = true;
            return false;
        }

        Batch<TRow> nextBatch;

        try
        {
            nextBatch = Pulse();
        }
        catch (JsonRpcException e)
        {
            Dispose();
            throw new TWResultSetException("internal error", e);
        }

        var rows = nextBatch.Rows;
        if (rows == null || rows.Count == 0)
        {
            RowNumber = 0;
            IsAfterLast = true;
            Exhausted = true;
            Closed = true;
            return false;
        }

        foreach (var row in rows)
            Cache.Enqueue(row);

        Exhausted = nextBatch.Exhausted;
        IsBeforeFirst = false;
        Current = Cache.Dequeue();
        RowNumber++;
        return true;
    }


    /// <returns>the current row</returns>
    /// <exception cref="TWResultSetException">
    ///     if this TWResultSet is closed, or if <see cref="Next"/> has not yet been called
    ///     or has already returned false.
    /// </exception>
    public TRow Get()
    {
        if (Closed) throw closedResultSetException;
        if (IsBeforeFirst) throw new TWResultSetException("Get() called before Next()");
        if (IsAfterLast) throw new TWResultSetException("Get() called after Next() returned false");
        return Current;
    }

    /// <exception cref="TimeoutException">if time waiting exceeds the timeout value</exception>
    public Batch<TRow> Pulse()
    {
        if (Timeout == null) return Client.Execute(NextBatchRequest, Handle, FetchSize);

        var future = Client.ExecuteAsync(NextBatchRequest, Handle, FetchSize);
        try
        {
            return future.WithTimeout(TimeSpan.FromSeconds((double)Timeout)).Result;
        }
        catch (TimeoutException e)
        {
            throw new TimeoutException("client timeout waiting for next batch of results");
        }
    }

    [MethodImpl(MethodImplOptions.Synchronized)]
    private void OnDispose()
    {
        try
        {
            Client.Execute(CloseResultSetRequest, Handle);
        }
        catch (JsonRpcException e)
        {
            Logging.Log("error closing result set: " + e);
        }
    }
}

/// <summary>
///     A TWResultSetException can be thrown from a number of methods due to improper use of a TWResultSet.
///     Those methods document the cases where such an exception can occur.
/// </summary>
public class TWResultSetException : TriggerwareClientException
{
    public TWResultSetException(string message) : base(message)
    {
    }

    public TWResultSetException(string message, Exception innerException) : base(message, innerException)
    {
    }
}