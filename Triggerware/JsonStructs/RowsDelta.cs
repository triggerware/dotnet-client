using System.Text.Json.Serialization;

namespace Triggerware.JsonStructs;

public class RowsDelta<T>
{
    /// <summary>
    /// New rows since last poll.
    /// </summary>
    [JsonPropertyName("added")] public T[] Added { get; set; }

    /// <summary>
    /// Rows that were removed since last poll.
    /// </summary>
    [JsonPropertyName("deleted")] public T[] Deleted { get; set; }
}