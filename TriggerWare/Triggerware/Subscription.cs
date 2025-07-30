using System.Text.Json;
using JsonRpc;
using Triggerware.Interfaces;

namespace Triggerware;

internal interface ISubscription : IDisposable
{
    void HandleNotificationFromBatch(object[] tuples);
    void RemoveFromBatch();
}

/// <summary>
///     A Subscription represents some future change to the data managed by the TW server about which a client would like
///     to
///     be notified. Once created, this subscription will accept notification from the server when a change occurs, then
///     calling the (overwritten) method <see cref="HandleNotification">HandleNotification</see>.
///     <para></para>
///     By default, subscriptions are active when they are created - they immediately are registered with the server and
///     will start receiving notifications. This behavior may be changed upon construction, or by calling either the
///     <see cref="Activate" /> or <see cref="Deactivate" /> methods.
///     <para></para>
///     Like most Triggerware objects, this class is disposable. When disposed, the subscription will be removed from the
///     server and no longer receive notifications.
///     <para></para>
///     Subscriptions may either be created by passing in a <see cref="TriggerwareClient" />, where it will be immediately
///     registered with the server, OR by passing in a <see cref="BatchSubscription" />. See
///     <see cref="BatchSubscription" />
///     documentation for more information.
/// </summary>
/// <typeparam name="T">The expected type of one data tuple.</typeparam>
public abstract class Subscription<T> : AbstractQuery<T>, ISubscription
{
    private readonly object _lock = new();

    public Subscription(TriggerwareClient client, IQuery query, bool active = true)
        : base(client, query)
    {
        Label = client.RegisterSubscription();
        BaseParameters["label"] = Label;

        if (active) Activate();
    }

    public Subscription(BatchSubscription batchSubscription, IQuery query)
        : base(batchSubscription.Client, query)
    {
        Label = Client.RegisterSubscription();
        BaseParameters["label"] = Label;

        AddToBatch(batchSubscription);
    }

    public string Label { get; }
    public bool Active { get; private set; }
    public BatchSubscription? Batch { get; private set; }

    public void Dispose()
    {
        Client.RemoveMethod(Label);
        if (Active) Deactivate();
        if (Batch != null) RemoveFromBatch();
        GC.SuppressFinalize(this);
    }

    /// <summary>
    ///     Used by a connected batch subscription to pass on received tuples. Not meant for direct use.
    /// </summary>
    public void HandleNotificationFromBatch(object[] tuples)
    {
        foreach (var tuple in tuples)
        {
            var input = JsonSerializer.Deserialize<T>(JsonSerializer.Serialize(tuple));
            if (input == null)
                throw new JsonException("failed to deserialize tuple");

            HandleNotificationTheadSafe(input);
        }
    }

    /// <summary>
    ///     Removes this subscription from its batch. Alternatively may call the
    ///     <see cref="BatchSubscription.RemoveSubscription{T}">RemoveSubscription</see> method.
    /// </summary>
    /// <exception cref="SubscriptionException"> If the subscription is not part of a batch.</exception>
    public void RemoveFromBatch()
    {
        lock (_lock)
        {
            if (Batch == null)
                throw new SubscriptionException("Subscription is not part of a batch.");

            var parameters = new Dictionary<string, object>(BaseParameters)
            {
                { "method", Batch.MethodName },
                { "combine", true }
            };

            Client.Call<object?>("unsubscribe", parameters);
            Batch.Subscriptions.Remove(Label);
            Batch = null;
        }
    }

    /// <summary>
    ///     Adds this subscription to a batch. Alternatively may call the <see cref="BatchSubscription" /> method
    ///     <see cref="BatchSubscription.AddSubscription{T}">AddSubscription</see>.
    /// </summary>
    /// <param name="batch">The batch to add this subscription to.</param>
    /// <exception cref="SubscriptionException">
    ///     On attempting to add to a batch when this subscription is already active, part of another batch, or
    ///     registered with a different instance of <see cref="TriggerwareClient" />
    /// </exception>
    public void AddToBatch(BatchSubscription batch)
    {
        lock (_lock)
        {
            if (Batch != null)
                throw new SubscriptionException("attempted to add a subscription that was already part of a batch.");
            if (Active)
                throw new SubscriptionException("attempted to add a subscription that is already active.");
            if (Client != batch.Client)
                throw new SubscriptionException(
                    "attempted to add a subscription that is registered on a separate Triggerware client.");

            var parameters = new Dictionary<string, object>(BaseParameters)
            {
                { "method", batch.MethodName },
                { "combine", true }
            };
            Client.Call<object?>("subscribe", parameters);
            batch.Subscriptions[Label] = this;
            Batch = batch;
        }
    }

    /// <summary>
    ///     Activates the subscription, enabling notifications to be sent from the server. Subscriptions that are part
    ///     of a batch cannot be deactivated or activated individually.
    /// </summary>
    /// <exception cref="SubscriptionException">When a subscription that is part of a batch is activated.</exception>
    /// <exception cref="JsonRpcException">When the internal call to "subscribe" fails.</exception>
    public void Activate()
    {
        lock (_lock)
        {
            if (Batch != null)
                throw new SubscriptionException("attempted to activate a subscription that was apart of a batch");
            if (Active)
                throw new SubscriptionException("Subscription is already active.");

            var parameters = new Dictionary<string, object>(BaseParameters)
            {
                { "method", Label },
                { "combine", false }
            };
            Client.Call<object?>("subscribe", parameters);
            Client.AddMethod(Label, HandleNotificationTheadSafe);
            Active = true;
        }
    }

    /// <summary>
    ///     Deactivates the subscription, preventing it from receiving further notifications. Subscriptions that are part
    ///     of a batch cannot be deactivated or activated individually.
    /// </summary>
    /// <exception cref="SubscriptionException">When a subscription that is part of a batch is deactivated.</exception>
    public void Deactivate()
    {
        lock (_lock)
        {
            if (Batch != null)
                throw new SubscriptionException("attempted to deactivate a subscription that was apart of a batch");
            if (!Active)
                throw new SubscriptionException("Subscription is already inactive.");

            var parameters = new Dictionary<string, object>(BaseParameters)
            {
                { "method", Label },
                { "combine", false }
            };
            Client.Call<object?>("unsubscribe", parameters);
            Client.RemoveMethod(Label);
            Active = false;
        }
    }

    /// <summary>
    ///     Overload this function in an inheriting class to handle notifications that the triggering query will cause
    ///     this subscription to receive.
    /// </summary>
    /// <param name="tuple">A data element from the subscription notification</param>
    public abstract void HandleNotification(T tuple);

    private void HandleNotificationTheadSafe(T tuple)
    {
        lock (_lock)
        {
            HandleNotification(tuple);
        }
    }
}

public class SubscriptionException(string message) : JsonRpcException(message, -32701)
{
}