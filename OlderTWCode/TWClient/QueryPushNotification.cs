using System.Text.Json.Serialization;
using TWClients.JsonRpcMessages;

namespace TWClients;

[method: JsonConstructor]
public class QueryPushNotification<TRow>(bool exhausted, List<TRow> tuples) : Notification
{
    public bool Exhausted => exhausted;
    [JsonPropertyName("tuples")] public List<TRow> Rows => tuples;

    public override void Handle(JsonRpcClient client, string notificationMethod)
    {
        var controller = (PushResultController<TRow>)this.Inducer;
        
        controller.HandleResults(Rows, exhausted);
    }
}