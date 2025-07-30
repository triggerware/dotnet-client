using System.Collections.ObjectModel;
using Triggerware.Interfaces;
using Triggerware.JsonStructs;

namespace Triggerware;

/// <summary>
///     A PreparedQuery is a query which may be executed multiple times, possibly with different parameters.
///     Unlike a <see cref="View{T}">View</see>, A PreparedQuery contains a handle to a Triggerware object, so a
///     PreparedQuery
///     both exists locally and on the Triggerware server.
///     <para></para>
///     A query string passed to a PreparedQuery may contain (for FOL queries) unbound values, or (for SQL queries) column
///     names prefaced with a ':?'. In either case, this indicates values that are currently not set and may be set to any
///     values using <see cref="SetParameter(string,object)">SetParameter</see> before re-executing the query.
/// </summary>
/// <inheritdoc cref="AbstractQuery{T}" />
public class PreparedQuery<T> : AbstractQuery<T>, IDisposable
{
    private readonly string[] _inputSignatureNames = null!;
    private readonly Type[][] _inputSignatureTypes = null!;
    private readonly object _lock = new();
    protected object[] PreparedParameters = null!;

    /// <inheritdoc />
    public PreparedQuery(TriggerwareClient client, IQuery query, QueryRestriction? restriction = null)
        : base(client, query, restriction)
    {
        var registration = Client.Call<PreparedQueryRegistration>("prepare-query", BaseParameters);
        PreparedParameters = new object[registration.InputSignatureNames.Length];
        _inputSignatureNames = registration.InputSignatureNames;
        _inputSignatureTypes = registration.InputSignatureTypes;
        UsesNamedParameters = registration.UsesNamedParameters ?? false;
        Handle = registration.Handle;
    }

    public bool IsDisposed { get; private set; }
    public bool UsesNamedParameters { get; }
    public string[] InputSignatureNames => _inputSignatureNames.ToArray();
    public Type[][] InputSignatureTypes => _inputSignatureTypes.Select(x => x.ToArray()).ToArray();

    public void Dispose()
    {
        IsDisposed = true;
        lock (_lock)
        {
            Client.Call<object?>("release-query", [Handle]);
            GC.SuppressFinalize(this);
        }
    }

    /// <summary>
    ///     Sets an unbound value in the query string to a specific value.
    /// </summary>
    /// <param name="name">The name of the value that appeared in the query.</param>
    /// <param name="param">The value to be set.</param>
    /// <exception cref="PreparedQueryException">If the set value does not match the expected type.</exception>
    public void SetParameter(string name, object param)
    {
        lock (_lock)
        {
            if (!UsesNamedParameters)
                throw new PreparedQueryException("This query does not use named parameters.");

            var i = Array.FindIndex(_inputSignatureNames, x => x == name);
            if (i == -1)
                throw new PreparedQueryException($"Parameter {name} not found in query signature.");

            if (Language == "sql" && !InputSignatureTypes[i].Contains(param.GetType()))
                throw new PreparedQueryException(
                    $"Expected type {string.Join(",", InputSignatureTypes[i].Select(x => x.Name))}, got type {param.GetType().Name}.");

            PreparedParameters[i] = param;
        }
    }

    /// <summary>
    ///     Sets an unbound value in the query string to a specific value.
    /// </summary>
    /// <param name="index">The index of the unset value of all unset values in the query.</param>
    /// <param name="param">The value to be set.</param>
    /// <exception cref="PreparedQueryException">If an incorrect parameter or index is used.</exception>
    public void SetParameter(int index, object param)
    {
        lock (_lock)
        {
            if (UsesNamedParameters)
                throw new PreparedQueryException("This query uses named parameters.");

            if (index < 0 || index >= _inputSignatureNames.Length)
                throw new PreparedQueryException($"Index {index} out of range.");

            if (Language == "sql" && !InputSignatureTypes[index].Contains(param.GetType()))
                throw new PreparedQueryException(
                    $"Expected type {string.Join(",", InputSignatureTypes[index].Select(x => x.Name))}, got type {param.GetType().Name}.");

            PreparedParameters[index] = param;
        }
    }

    /// <summary>
    /// Gets the value of a parameter in the query.
    /// </summary>
    /// 
    /// <param name="index">The index of the parameter.</param>
    /// <returns>the value of the parameter</returns>
    /// <exception cref="PreparedQueryException">If an incorrect index is used.</exception>
    public object GetParameter(int index, object param)
    {
        lock (_lock)
        {
            if (UsesNamedParameters)
                throw new PreparedQueryException("This query uses named parameters.");

            if (index < 0 || index >= _inputSignatureNames.Length)
                throw new PreparedQueryException($"Index {index} out of range.");
            
            return PreparedParameters[index];
        }
    }
    
    /// <summary>
    /// Gets the value of a parameter in the query.
    /// </summary>
    /// <param name="name">The name of the parameter.</param>
    /// <returns>the value of the parameter</returns>
    /// <exception cref="PreparedQueryException">If an incorrect index is used.</exception>
    public object GetParameter(string name, object param)
    {
        lock (_lock)
        {
            if (!UsesNamedParameters)
                throw new PreparedQueryException("This query does not use named parameters.");

            var i = Array.FindIndex(_inputSignatureNames, x => x == name);
            if (i == -1)
                throw new PreparedQueryException($"Parameter {name} not found in query signature.");

            return PreparedParameters[i];
        }
    }

    /// <summary>
    ///     Clones this prepared query and returns a new instance with the same parameters.
    /// </summary>
    /// <returns>A copy of this.</returns>
    public PreparedQuery<T> CloneWithParameters()
    {
        lock (_lock)
        {
            var restriction = new QueryRestriction(RowLimit, Timeout);
            var clone = new PreparedQuery<T>(Client, this, restriction);
            clone.PreparedParameters = (object[])PreparedParameters.Clone();
            return clone;
        }
    }

    /// <summary>
    ///     Clears all previously set parameters in this query.
    /// </summary>
    public void ClearParameters()
    {
        lock (_lock)
        {
            for (var i = 0; i < PreparedParameters.Length; i++)
                PreparedParameters[i] = new object();
        }
    }

    /// <summary>
    ///     Executes this query on the connected Triggerware server.
    /// </summary>
    /// <param name="restriction">An optional <see cref="QueryRestriction" /> to control what is needed for this query. </param>
    /// <returns>A <see cref="ResultSet{T}" /> that stores the rows provided by the server.</returns>
    /// <exception cref="JsonRpc.JsonRpcException">When the internal call to 'create-resultset' fails.</exception>
    public ResultSet<T> Execute(QueryRestriction? restriction = null)
    {
        lock (_lock)
        {
            var parameters = new Dictionary<string, object>
            {
                { "handle", Handle! },
                { "inputs", PreparedParameters }
            };
            if (restriction != null)
            {
                if (restriction.Value.RowLimit.HasValue) parameters["limit"] = restriction.Value.RowLimit.Value;
                if (restriction.Value.Timeout.HasValue) parameters["timelimit"] = restriction.Value.Timeout.Value;
            }

            var result = Client.Call<ExecuteQueryResult<T>>("create-resultset", parameters);
            return new ResultSet<T>(Client, result);
        }
    }
}

internal class PreparedQueryException(string message) : Exception(message)
{
}