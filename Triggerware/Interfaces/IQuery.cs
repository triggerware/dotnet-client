namespace Triggerware.Interfaces;

/// <summary>
///     Represents a raw query that can be executed by a Triggerware server.
///     <para></para>
///     Will Usually be constructed as either an <see cref="FolQuery" /> or <see cref="SqlQuery" />.
/// </summary>
public interface IQuery
{
    /// <summary>
    /// A query string written in the specified language.
    /// </summary>
    public string Query { get; }
    
    /// <summary>
    /// The language this query is written in, either "sql" or "fol".
    /// </summary>
    public string Language { get; }
    
    /// <summary>
    /// The namespace of this query is written in.
    /// </summary>
    public string Schema { get; }
}