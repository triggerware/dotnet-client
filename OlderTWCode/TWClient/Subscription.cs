using System.Runtime.CompilerServices;
using TWClients.JsonRpcMessages;

namespace TWClients;

public interface ISubscription
{
    public Type RowType { get; }

    public TriggerwareClient? ActiveConnection();
    
    public BatchSubscription? PartOfBatch { get; set; }

    public void RegisterWithTw(TriggerwareClient client);

    public void UnregisterWithTw();
    public void HandleBatchNotifications(object tuples);
}

public abstract class Subscription<TRow> : ISubscription, INotificationInducer
{
    private static readonly NamedParameterRequest<VoidType> SubscribeRequest =
        new("subscribe", new[] { "description", "language", "package", "label", "method" },
            new[] { "combine" });

    private static readonly NamedParameterRequest<VoidType> UnsubscribeRequest =
        new("unsubscribe", new[] { "description", "label", "method" }, null);

    public static readonly int SubscriptionErrorCode = -32701;

    public Subscription(string triggeringConditon, string schema)
        : this(triggeringConditon, schema, TWClients.Language.Sql)
    {
    }

    public Subscription(string triggeringConditon, string? schema, string language)
    {
        TriggeringConditon = triggeringConditon;
        Schema = schema;
        NamedParameters = new Dictionary<string, object>
        {
            { "query", triggeringConditon },
            { "language", language },
            { "label", NotificationTag }
        };
        if (schema != null) NamedParameters["namespace"] = schema;
    }

    public TriggerwareClient? SubscribedOn { get; private set; }
    public string TriggeringConditon { get; }
    public string? NotificationTag { get; } = TriggerwareClient.NextNotificationMethod("sub");
    private string Language { get; }

    public BatchSubscription? PartOfBatch { get; set; } = null;
    public Dictionary<string, object?> NamedParameters { get; }
    public string Schema { get; }

    public Type NotificationType => RowType;

    public Type RowType => typeof(TRow);

    [MethodImpl(MethodImplOptions.Synchronized)]
    public TriggerwareClient ActiveConnection()
    {
        if (PartOfBatch != null) return PartOfBatch.ActiveConnection();
        return SubscribedOn;
    }

    /// <summary>
    ///     Activate a subscription for individual notifications
    /// </summary>
    /// <param name="client">
    ///     the client which should monitor this subscription.
    ///     Notifications will arrive on the client's primary connection
    ///     <ul>
    ///         <li> already has this subscription active on a different connection</li>
    ///         <li> this subscription is part of a BatchSubscription </li>
    ///     </ul>
    /// </param>
    /// <exception cref="JsonRpcException">if the activation fails for any other reason</exception>
    public void Activate(TriggerwareClient client)
    {
        if (PartOfBatch != null)
            throw new SubscriptionException(
                "attempt to activate a subscription as an individual subscription when it is part of a batch", this);
        if (SubscribedOn == client) return;
        if (SubscribedOn != null)
            throw new SubscriptionException("subscription is already active on a different connection", this);
        client.RegisterNotificationInducer(NotificationTag, this);
        try
        {
            RegisterWithTw(client);
        }
        catch (JsonRpcException e)
        {
            client.UnregisterNotificationInducer(NotificationTag);
            throw;
        }
    }

    /// <summary>
    ///     Deactivate this subscription so that the TW server ceases to monitor for changes in its condition and
    ///     sends no further notifications of such changes.  This is a noop if this subscription is not currently active.
    /// </summary>
    /// <exception cref="SubscriptionException">if this subscription is part of a BatchSubscription</exception>
    /// <exception cref="JsonRpcException">if the deactivation fails for any reason</exception>
    public void Deactivate()
    {
        if (PartOfBatch != null)
            throw new SubscriptionException(
                "attempt to deactivate a subscription as an individual subscription when it is part of a batch", this);
        UnregisterWithTw();
        SubscribedOn.UnregisterNotificationInducer(NotificationTag);
    }

    public void UnregisterWithTw()
    {
        SubscribedOn?.Execute(UnsubscribeRequest, NamedParameters);
        SubscribedOn = null;
    }

    public void RegisterWithTw(TriggerwareClient client)
    {
        NamedParameters["combine"] = PartOfBatch != null;
        NamedParameters["method"] = PartOfBatch == null ? NotificationTag : PartOfBatch.NotificationTag;
        client.Execute(SubscribeRequest, NamedParameters);
        SubscribedOn = client;
    }

    /// <summary>
    ///     handleNotification is called to respond to a triggering of the subscription's condition. Any instantiable subclass
    ///     of Subscription must implement this method.  The method may not throw any <em>checked</em> exceptions. If a
    ///     handleNotification
    ///     method throws an <em>unchecked</em> exception, the exception will be logged and ignored by the TriggerwareClient.
    /// </summary>
    /// <param name="tuple">
    ///     the object allocated to hold the 'tuple' of values that represents a triggering of the
    ///     subscription's condition.
    /// </param>
    public abstract void HandleNotification(TRow tuple);

    /// <summary>
    ///     handleNotificationFromBatch is called to respond to a triggering of the subscription's condition when the
    ///     subscription
    ///     is part of a BatchSubscription and the <em>default</em> handling of the batch subscription is being employed.
    ///     The default implementation of handleNotificationFromBatch simply calls the subscription's handleNotification
    ///     method.
    /// </summary>
    /// <param name="tuple">
    ///     the object allocated to hold the 'tuple' of values that represents a triggering of the
    ///     subscription's condition.
    /// </param>
    public void HandleNotificationFromBatch(TRow tuple)
    {
        HandleNotification(tuple);
    }

    public void HandleBatchNotifications(IEnumerable<TRow> tuples)
    {
        foreach (var tuple in tuples) HandleNotificationFromBatch(tuple);
    }

    public void HandleBatchNotifications(object tuples)
    {
        if (tuples is IEnumerable<TRow> ts)
            HandleBatchNotifications(ts);
        else
            throw new ArgumentException("expected IEnumerable<" + typeof(TRow).Name + ">");        
    }

    public class SubscriptionException : JsonRpcException
    {
        public SubscriptionException(string problem, ISubscription s)
            : base(problem, SubscriptionErrorCode)
        {
            Subscription = s;
        }

        public SubscriptionException(string problem)
            : base(problem, SubscriptionErrorCode)
        {
            Subscription = null;
        }

        /// <returns>the subscription that encountered a problem.</returns>
        public ISubscription? Subscription { get; }
    }
}