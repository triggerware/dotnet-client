using Triggerware.Interfaces;

namespace Triggerware;

/// <summary>
///     A raw FOL query.
/// </summary>
public class FolQuery(string query, string schema = "AP5") : IQuery
{
    public string Query { get; } = query;
    public string Language => "fol";
    public string Schema { get; } = schema;
}