namespace Triggerware.Interfaces;

public interface IResourceRestricted
{
    /// <summary>
    /// The maximum number of rows that can be returned by the server at one time.
    /// </summary>
    public uint? RowLimit { get; }
    /// <summary>
    /// A time limit for a query response.
    /// </summary>
    public double? Timeout { get; }
}