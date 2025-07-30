using Triggerware.Interfaces;
using Triggerware.JsonStructs;

namespace Triggerware;

/// <summary>
///     A simple reusable view of a query. May be executed any number of times. Unlike other queries such as
///     <see cref="PreparedQuery{T}" />,
///     Views do not have a handle to a triggerware server and are only stored locally.
/// </summary>
/// <inheritdoc cref="AbstractQuery{T}" />
public class View<T> : AbstractQuery<T>
{
    private readonly object _lock = new();

    /// <inheritdoc />
    public View(TriggerwareClient client, IQuery query, QueryRestriction? restriction = null)
        : base(client, query, restriction)
    {
        client.ValidateQuery(this);
    }

    /// <summary>
    ///     Executes this query on the connected triggerware server.
    /// </summary>
    /// <param name="restriction">An optional <see cref="QueryRestriction" /> to control what is needed for this query. </param>
    /// <returns>A <see cref="ResultSet{T}" /> that stores the rows provided by the server.</returns>
    /// <exception cref="JsonRpc.JsonRpcException">If the internal JsonRpc method call fails.</exception>
    public ResultSet<T> Execute(QueryRestriction? restriction = null)
    {
        lock (_lock)
        {
            ExecuteQueryResult<T>? eqResult;
            if (restriction == null)
            {
                eqResult = Client.Call<ExecuteQueryResult<T>>("execute-query", BaseParameters);
                return new ResultSet<T>(Client, eqResult);
            }

            var parameters = new Dictionary<string, object>(BaseParameters);
            if (restriction.Value.RowLimit != null) parameters["limit"] = restriction.Value.RowLimit;
            if (restriction.Value.Timeout != null) parameters["timelimit"] = restriction.Value.Timeout;
            eqResult = Client.Call<ExecuteQueryResult<T>>("execute-query", parameters);
            return new ResultSet<T>(Client, eqResult);
        }
    }
}