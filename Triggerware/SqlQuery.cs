using Triggerware.Interfaces;

namespace Triggerware;

/// <summary>
///     A raw SQL query.
/// </summary>
public class SqlQuery(string query, string schema = "AP5") : IQuery
{
    public string Query { get; } = query;
    public string Language => "sql";
    public string Schema { get; } = schema;
}