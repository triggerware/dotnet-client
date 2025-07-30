namespace TWClients.JsonRpcMessages;

public interface INotificationInducer
{
    public Type NotificationType { get; }

    public bool IsClosed()
    {
        return false;
    }
}