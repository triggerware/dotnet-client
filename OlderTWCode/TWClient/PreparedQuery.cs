using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;
using Newtonsoft.Json.Linq;
using TWClients.JsonRpcMessages;

namespace TWClients;

/// <summary>
///     This interface is just to mimic Java's type erasure with '?' generics.
/// </summary>
public interface IPreparedQuery
{
    public Type RowType { get; }
    public bool Close();
}

/// <summary>
///     A PreparedQuery represents a parametrized query that can be issued repeatedly on a connection
///     with different values of its parameters.
///     <para></para>
///     Parameters in an SQL query can be named or positional.
/// </summary>
/// <typeparam name="TRow">the row type for results of the PreparedQuery</typeparam>
public class PreparedQuery<TRow> : AbstractQuery<TRow>, IStatement, IPreparedQuery
{
    private static readonly object Unset = new();
    private int _nParams;
    private int _nParamSet;
    private bool _usesNamedParameters;

    /// <summary>
    ///     Creates a PreparedQuery and registers it for use on a client's primary connection.
    /// </summary>
    /// <param name="query">The SQL query containing input placeholders.</param>
    /// <param name="schema">The default schema for the query.</param>
    /// <param name="client">The client that will use this query on its primary connection.</param>
    /// <exception cref="JsonRpcException">
    ///     Thrown if the server refuses the request to create a prepared query for the query/schema values supplied.
    /// </exception>
    public PreparedQuery(string query, string schema, TriggerwareClient client)
        : this(query, TWClients.Language.Sql, schema, client)
    {
    }

    /// <summary>
    ///     Creates a PreparedQuery and registers it for use on a connection.
    /// </summary>
    /// <param name="query">The SQL query containing input placeholders.</param>
    /// <param name="language">The appropriate members of <see cref="Language" /> for the query.</param>
    /// <param name="schema">The default schema for the query.</param>
    /// <param name="client">The client for which this PreparedQuery will be registered and later used.</param>
    /// <exception cref="JsonRpcException">
    ///     Thrown if the server refuses the request to create a prepared query for the
    ///     query/schema values supplied.
    /// </exception>
    public PreparedQuery(string query, string language, string schema, TriggerwareClient client) : base(query, language,
        schema)
    {
        Client = client;
        RegisterNoCheck();
    }

    public Type?[] InputSignatureTypes { get; private set; }
    public Type?[] OutputSignatureTypes { get; private set; }
    public string?[]? InputSignatureNames { get; private set; }
    public string?[] OutputSignatureNames { get; private set; }
    public string?[] InputSignatureTypeNames { get; private set; }
    public string?[] OutputSignatureTypeNames { get; private set; }
    public object[]? ParamsByIndex { get; private set; }
    public int? FetchSize { get; set; }
    public HashSet<TWResultSet<TRow>> Outstanding { get; } = [];

    private static PositionalParameterRequest<VoidType> ReleaseQueryRequest { get; } =
        new("release-query", 1, 1);

    public Type RowType => typeof(TRow);
    
    public bool Close()
    {
        return CloseQuery();
    }

    public JsonRpcClient Client { get; }

    public void Dispose()
    {
        CloseQuery();
        GC.SuppressFinalize(this);
    }
    
    [MethodImpl(MethodImplOptions.Synchronized)]
    protected void RegisterNoCheck()
    {
        var parameters = new Dictionary<string, object?>
        {
            { "query", Query },
            { "language", Language },
            { "namespace", Schema }
        };
        var pqResult =
            Client!.Rpc.InvokeWithParameterObjectAsync<PreparedQueryRegistration>("prepare-query",
                parameters).Result;

        RecordRegistration((TriggerwareClient)Client, pqResult.Handle);
        FetchSize = ((TriggerwareClient)Client).DefaultFetchSize;
        InputSignatureTypes = pqResult.InputTypeSignature;
        InputSignatureTypeNames = pqResult.InputTypeNames;
        OutputSignatureNames = SignatureNames(pqResult.Signature);
        OutputSignatureTypes = pqResult.OutputTypeSignature;
        OutputSignatureTypeNames = pqResult.OutputTypeNames;
        _usesNamedParameters = pqResult.UsesNamedParameters;
        _nParams = InputSignatureTypes.Length;

        InputSignatureNames = _usesNamedParameters ? SignatureNames(pqResult.InputSignature) : null;

        ParamsByIndex = new object[_nParams];
        ClearParameters();
    }

    [MethodImpl(MethodImplOptions.Synchronized)]
    protected void Register()
    {
        if (Client != null)
            throw new ReregistrationException();
        RegisterNoCheck();
    }

    [MethodImpl(MethodImplOptions.Synchronized)]
    public void ClearParameters()
    {
        if (ParamsByIndex == null)
            return;
        for (var i = 0; i < _nParams; i++)
            ParamsByIndex[i] = Unset;
        _nParamSet = 0;
    }

    [MethodImpl(MethodImplOptions.Synchronized)]
    public bool FullyInstantiated()
    {
        return _nParamSet == _nParams;
    }

    /// <summary>
    ///     Set a parameter of a PreparedQuery that used positional parameters.
    /// </summary>
    /// <param name="parameterIndex">the index of the parameter to set</param>
    /// <param name="paramValue">the parameter value to use</param>
    /// <returns>this PreparedQuery. This is useful for chaining the setting of multiple parameters.</returns>
    /// <exception cref="TriggerwareClientException">
    ///     if this PreparedQuery uses named parameters,
    ///     or if the parameterName is not the name of one of this PreparedQuery's parameters,
    ///     or if the parameter value is not of an acceptable type for the parameter's uses in the query.
    /// </exception>
    [MethodImpl(MethodImplOptions.Synchronized)]
    public PreparedQuery<TRow> SetParameter(int parameterIndex, object paramValue)
    {
        if (_usesNamedParameters)
            throw new TriggerwareClientException(
                "cannot set parameter of a named PreparedQuery via an integer index");
        if (parameterIndex < 1 || parameterIndex > _nParams)
            throw new TriggerwareClientException("prepared query parameter index out of bounds");

        var type = InputSignatureTypes[parameterIndex - 1];

        if (type != null && !type.IsInstanceOfType(paramValue))
            throw new TriggerwareClientException(
                "prepared query parameter type error for parameter index " + parameterIndex);

        var old = ParamsByIndex?[parameterIndex - 1];
        if (ParamsByIndex != null) ParamsByIndex[parameterIndex - 1] = paramValue;
        if (old == Unset) _nParamSet++;
        return this;
    }

    /// <summary>
    ///     Set a parameter of a PreparedQuery that used named parameters.
    /// </summary>
    /// <param name="parameterName">the name of the parameter to set</param>
    /// <param name="paramValue">the parameter value to use</param>
    /// <returns>this PreparedQuery. This is useful for chaining the setting of multiple parameters.</returns>
    /// <exception cref="TriggerwareClientException">
    ///     if this PreparedQuery uses positional parameters,
    ///     or if the parameterName is not the name of one of this PreparedQuery's parameters,
    ///     or if the parameter value is not of an acceptable type for the parameter's uses in the query.
    /// </exception>
    [MethodImpl(MethodImplOptions.Synchronized)]
    public PreparedQuery<TRow> SetParameter(string parameterName, object paramValue)
    {
        if (!_usesNamedParameters)
            throw new TriggerwareClientException(
                "cannot set parameter of an indexed parameter PreparedQuery via a parameter name");
        var parameterIndex = IndexOfParameterName(parameterName);
        if (parameterIndex == -1)
            throw new TriggerwareClientException("unknown prepared query parameter name " +
                                                 parameterName);

        var type = InputSignatureTypes[parameterIndex];
        if (type != null && !type.IsInstanceOfType(paramValue))
            throw new TriggerwareClientException(
                "prepared query parameter type error for parameter name " + parameterName);

        var old = ParamsByIndex?[parameterIndex];
        if (ParamsByIndex != null) ParamsByIndex[parameterIndex] = paramValue;
        if (old == Unset) _nParamSet++;
        return this;
    }

    private int IndexOfParameterName(string paramName)
    {
        if (InputSignatureNames == null)
            return -1;

        var i = 0;
        foreach (var name in InputSignatureNames)
            if (name?.ToLower() == paramName.ToLower()) return i;
            else i++;

        return -1;
    }

    public TWResultSet<TRow>? ExecuteQuery()
    {
        try
        {
            var rs = CreateResultSet(null);
            Outstanding.Add(rs);
            return rs;
        }
        catch (TimeoutException e)
        {
            return null;
        }
    }

    public TWResultSet<TRow>? ExecuteQuery(QueryResourceLimits qrl)
    {
        var rs = CreateResultSet(qrl);
        Outstanding.Add(rs);
        return rs;
    }

    [MethodImpl(MethodImplOptions.Synchronized)]
    private TWResultSet<TRow> CreateResultSet(QueryResourceLimits? qrl)
    {
        Dictionary<string, object?>? parameters;
        int? fetchSize;

        lock (this)
        {
            if (Closed)
                throw new TriggerwareClientException("attempt to execute a closed PreparedQuery");

            if (!FullyInstantiated())
                throw new TriggerwareClientException(
                    "cannot execute a prepared query without setting all the parameters");

            fetchSize = FetchSize;
            var timeout = qrl?.Timeout;
            if (qrl is { RowCountLimit: not null })
                fetchSize = qrl.RowCountLimit;

            parameters = new Dictionary<string, object?>
            {
                { "handle", TwHandle },
                { "limit", fetchSize },
                { "timelimit", timeout },
                { "inputs", ParamsByIndex },
                { "check-update", false }
            };
        }

        var eqResult = Client.Rpc
            .InvokeWithParameterObjectAsync<ExecuteQueryResult<TRow>>("create-resultset", parameters).Result;
        return new TWResultSet<TRow>(this, fetchSize, eqResult, this);
    }

    public override object Clone()
    {
        var clone = (PreparedQuery<TRow>)MemberwiseClone();
        ((TriggerwareClient)clone.Client).AddPreparedQuery(clone);
        return clone;
    }

    [MethodImpl(MethodImplOptions.Synchronized)]
    public override bool CloseQuery()
    {
        if (Closed) return false;
        try
        {
            foreach (var rs in Outstanding) rs.Dispose();
            Closed = true;
            Outstanding.Clear();
            Client.Execute(ReleaseQueryRequest, TwHandle);
            ((TriggerwareClient)Client).RemovePreparedQuery(this);
        }
        catch (JsonRpcException e)
        {
            Logging.Log("error closing prepared query: " + e);
        }

        return true;
    }

    [method: JsonConstructor]
    public class PreparedQueryRegistration(
        int handle,
        SignatureElement[] signature,
        SignatureElement[] inputSignature,
        bool usesNamedParameters)
    {
        public SignatureElement[] InputSignature => inputSignature;
        public SignatureElement[] Signature => signature;
        public int Handle => handle;
        public bool UsesNamedParameters => usesNamedParameters;

        [JsonIgnore] public Type?[] InputTypeSignature => TypeSignatureTypes(InputSignature);

        [JsonIgnore] public Type?[] OutputTypeSignature => TypeSignatureTypes(Signature);

        [JsonIgnore] public string?[] InputTypeNames => TypeSignatureTypeNames(InputSignature);

        [JsonIgnore] public string?[] OutputTypeNames => TypeSignatureTypeNames(Signature);
    }
}