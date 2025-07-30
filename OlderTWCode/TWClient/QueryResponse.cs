using System.Text.Json.Serialization;

namespace TWClients;

[method: JsonConstructor]
public class QueryResponse<TRow>(
    List<TRow> result,
    string[] columns,
    string exitstatus)
{
    public List<TRow> Result => result;
    public string[] Columns => columns;

    [JsonPropertyName("exitstatus")] public string CompletionStatus => exitstatus;
}