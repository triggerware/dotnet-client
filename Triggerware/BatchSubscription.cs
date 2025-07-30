using Triggerware.Interfaces;
using Triggerware.JsonStructs;

namespace Triggerware;

/// <summary>
///     A BatchSubscription groups one or more <see cref="Subscription{T}">Subscription</see> instances. Over time, new
///     instances may be added to the BatchSubscription, and/or existing members may be removed. This is useful because
///     a single transaction of a change in data on the triggerware server may be associated with multiple subscriptions.
///     <para></para>
///     By grouping these subscriptions, notifications may be properly handled by as many
///     <see cref="Subscription{T}">Subscription</see> instances as necessary.
///     <para></para>
///     When a BatchSubscription is disposed, all of its subscriptions are removed from it and become deactivated. They
///     may later be activated or added to a separate batch subscription.
/// </summary>
public class BatchSubscription : IDisposable, ITriggerwareObject
{
    internal readonly Dictionary<string, ISubscription> Subscriptions = [];
    private readonly object _lock = new();

    public BatchSubscription(TriggerwareClient client)
    {
        Client = client;
        MethodName = client.RegisterBatchSubscription();
        Client.AddMethod(MethodName, HandleNotification);
    }

    public string MethodName { get; }

    /// <summary>
    ///     Disposes of this BatchSubscription. All subscriptions are removed from the batch and deactivated.
    /// </summary>
    public void Dispose()
    {
        Client.RemoveMethod(MethodName);
        foreach (var sub in Subscriptions.Values) sub.RemoveFromBatch();
        GC.SuppressFinalize(this);
    }

    public TriggerwareClient Client { get; }
    public long? Handle => null;

    /// <summary>
    ///     For Subscriptions, this method will be overwritten to handle notifications. This method will call each
    ///     subscription's
    ///     <see cref="Subscription{T}.HandleNotification" /> method.
    /// </summary>
    /// <param name="notification"></param>
    public void HandleNotification(CombinedSubscriptionNotification notification)
    {
        lock (_lock)
        {
            foreach (var match in notification.Matches)
                if (Subscriptions.TryGetValue(match.Label, out var sub))
                    sub.HandleNotificationFromBatch(match.Tuples);
        }
    }

    /// <summary>
    ///     Adds the specified subscription to this batch. Alternatively, you may call the instance method
    ///     <see cref="Subscription{T}.AddToBatch" />.
    /// </summary>
    /// <param name="subscription">The subscription to add to this batch.</param>
    /// <exception cref="SubscriptionException">
    ///     Thrown when the subscription is already active, part of another batch, or registered with a different
    ///     instance of <see cref="TriggerwareClient" />.
    /// </exception>
    public void AddSubscription<T>(Subscription<T> subscription)
    {
        lock (_lock)
        {
            subscription.AddToBatch(this);
        }
    }

    /// <summary>
    ///     Removes a subscription from this batch. Alternatively, you may call the instance method
    ///     <see cref="Subscription{T}.RemoveFromBatch" />.
    /// </summary>
    /// <param name="subscription">The subscription to remove.</param>
    /// <exception cref="SubscriptionException"> if the subscription is not part of this batch. </exception>
    public void RemoveSubscription<T>(Subscription<T> subscription)
    {
        lock (_lock)
        {
            if (subscription.Batch != this)
                throw new SubscriptionException("Subscription is not part of this batch.");
            subscription.RemoveFromBatch();
        }
    }
}