using System.Runtime.CompilerServices;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using TWClients.JsonRpcMessages;

namespace TWClients;

/// <summary>
/// A superclass for various forms of query for which TW provides a handle to allow the client to use the query.
/// In all cases, the handle is returned from some request on a connection, and the handle is only valid for requests
/// submitted on that connection.
/// </summary>
/// <typeparam name="TRow">the class that represents a single 'row' of the answer to the query.</typeparam>
public abstract class AbstractQuery<TRow>(string query, string language, string schema) : ICloneable
{
    public static readonly Dictionary<string, Type> ColumnTypes = new()
    {
        { "double", typeof(double) },
        { "integer", typeof(int) },
        { "number", typeof(float) },
        { "boolean", typeof(bool) },
        { "stringcase", typeof(string) },
        { "stringnocase", typeof(string) },
        { "stringagnostic", typeof(string) },
        { "date", typeof(DateTime) },
        { "time", typeof(DateTime) },
        { "timestamp", typeof(DateTimeOffset) },
        { "interval", typeof(TimeSpan) },
        { "", typeof(object) }
    };

    public AbstractQuery(string query, string schema)
        : this(query, TWClients.Language.Sql, schema)
    {
    }
    
    public AbstractQuery(AbstractQuery<TRow> aq)
        : this(aq.Query, aq.Language, aq.Schema)
    {
    }

    public string Query => query;
    public string Language => language;
    public string Schema => schema;
    public int? TwHandle { get; protected set; }
    public bool Closed { get; protected set; } = false;
    public TriggerwareClient Client { get; protected set; }

    public abstract bool CloseQuery();

    /// <summary>
    /// Override this method in a subclass if your application asks for query results to be returned by notification.
    /// </summary>
    public virtual void ProcessNotificationResultSet(TWResultSet<TRow> resultSet)
    {
    }
    
    /// <summary>
    /// Override this method in a subclass if your application asks for query results to be returned by notification.
    /// </summary>
    public virtual void ProcessNotificationError(JToken error)
    {
    }

    protected void RecordRegistration(TriggerwareClient client, int twHandle)
    {
        TwHandle = twHandle;
        Client = client;
    }

    public static string?[] SignatureNames(SignatureElement[] sig)
    {
        return sig.Select(elem => elem.Name[0] == '?' ? elem.Name[1..] : elem.Name).ToArray();
    }

    public static Type?[] TypeSignatureTypes(SignatureElement[] sig)
    {
        return sig.Select(elem =>
        {
            if (ColumnTypes.TryGetValue(elem.TypeName, out var value))
                return value;

            Logging.Log("unknown type name from TW " + elem.TypeName);
            return null;
        }).ToArray();
    }

    public static string[] TypeSignatureTypeNames(SignatureElement[] sig)
    {
        return sig.Select(elem => elem.TypeName).ToArray();
    }
    public class ReregistrationException() : JsonRpcException("Attempted to register a method that is already registered.")
    {
    }

    public abstract object Clone();
}

[method: JsonConstructor]
public class SignatureElement(string attribute, string type)
{
    [JsonProperty("attribute")] public string Name => attribute;
    [JsonProperty("type")] public string TypeName => type;
}

