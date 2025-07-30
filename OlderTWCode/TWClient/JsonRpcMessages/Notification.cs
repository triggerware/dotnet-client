using System.Text.Json.Serialization;

namespace TWClients.JsonRpcMessages;

public abstract class Notification
{
    [JsonIgnore]
    public INotificationInducer Inducer { get; set; }
    public abstract void Handle(JsonRpcClient client, string methodName);
}