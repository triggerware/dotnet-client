using Triggerware.Interfaces;

namespace Triggerware;

/// <summary>
/// A resource restriction on queries executed on a triggerware server.
/// </summary>
/// <param name="rowLimit">A limit to how many rows the server can return at a time.</param>
/// <param name="timeout">A time limit for a query response.</param>
public readonly struct QueryRestriction(uint? rowLimit, double? timeout) : IResourceRestricted
{
    public uint? RowLimit { get; } = rowLimit;
    public double? Timeout { get; } = timeout;
}