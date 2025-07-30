using System.Collections.ObjectModel;
using System.Runtime.CompilerServices;
using TWClients.JsonRpcMessages;

namespace TWClients;

public abstract class PushResultController<TRow> : INotificationInducer
{
    private readonly int _notifyLimit;
    private readonly TimeSpan? _notifyTimeLimit;
    private readonly int? _rowLimit;
    public string? NotificationMethod = TriggerwareClient.NextNotificationMethod("__rsPush");

    public PushResultController(int notifyLimit, TimeSpan notifyTimeLimit, int? rowLimit)
    {
        if (notifyLimit <= 0)
            throw new ArgumentException("notifyLimit for a QueryResultController must be positive");

        _rowLimit = rowLimit;
        _notifyLimit = notifyLimit;
        _notifyTimeLimit = notifyTimeLimit;
    }

    public bool Closed { get; } = false;
    public bool CloseRequested { get; private set; }
    public int? Handle { get; private set; }
    public TriggerwareClient Client { get; private set; }
    public Type NotificationType { get; } = typeof(TRow);
    public abstract void HandleResults(IEnumerable<TRow> rows, bool exhausted);

    public Dictionary<string, object?> GetParams()
    {
        var parameters = new Dictionary<string, object?>
        {
            { "limit", _rowLimit },
            { "method", NotificationMethod },
            { "notify-limit", _notifyLimit }
        };
        if (_notifyTimeLimit != null)
            parameters["notify-timelimit"] = _notifyTimeLimit?.TotalSeconds;
        return parameters;
    }

    [MethodImpl(MethodImplOptions.Synchronized)]
    public void SetHandle(TriggerwareClient client, int handle)
    {
        Handle = handle;
        Client = client;

        if (CloseRequested) Close();
        else
            Client.RegisterNotificationInducer(NotificationMethod, this);
    }

    [MethodImpl(MethodImplOptions.Synchronized)]
    public void Close()
    {
        if (Closed) return;

        if (Handle == null)
        {
            CloseRequested = true;
            return;
        }

        CloseRequested = false;

        try
        {
            Client.Execute(TWResultSet<TRow>.CloseResultSetRequest, Handle);
        }
        catch (JsonRpcException e)
        {
            Logging.Log("Error closing a TWResultSet " + e.Message);
        }

        Client.UnregisterNotificationInducer(NotificationMethod);

        Handle = null;
    }
}