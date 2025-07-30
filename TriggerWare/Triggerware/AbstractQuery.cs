using Triggerware.Interfaces;

namespace Triggerware;

/// <typeparam name="T">The class that represents a single 'row' of the answer to the query.</typeparam>
public abstract class AbstractQuery<T>
    : ITriggerwareObject, IQuery, IResourceRestricted
{
    protected readonly Dictionary<string, object> BaseParameters;

    /// <param name="client">A client connected to a Triggerware server to register this query on.</param>
    /// <param name="query">
    ///     An object containing a query string and language/namespace restriction. Recommended to be either a
    ///     <see cref="SqlQuery" /> or a <see cref="FolQuery" />.
    /// </param>
    /// <param name="restriction">A <see cref="QueryRestriction" /> to control what is needed for this query. </param>
    public AbstractQuery(TriggerwareClient client, IQuery query, QueryRestriction? restriction = null)
    {
        Client = client;
        Query = query.Query;
        Language = query.Language;
        Schema = query.Schema;
        BaseParameters = new Dictionary<string, object>
        {
            { "query", query.Query },
            { "language", query.Language },
            { "namespace", query.Schema }
        };

        if (restriction == null) return;

        RowLimit = restriction.Value.RowLimit;
        Timeout = restriction.Value.Timeout;

        if (RowLimit.HasValue) BaseParameters["limit"] = RowLimit.Value;
        if (Timeout.HasValue) BaseParameters["timelimit"] = Timeout.Value;
    }

    public string Query { get; }
    public string Language { get; }
    public string Schema { get; }

    public uint? RowLimit { get; }
    public double? Timeout { get; }

    public TriggerwareClient Client { get; }
    public long? Handle { get; protected set; }
}

public class AbstractQueryException(string message, Exception? inner = null)
    : TriggerwareClientException(message, inner)
{
}

public class InvalidQueryException(string message, Exception? inner = null) : AbstractQueryException(message, inner);




