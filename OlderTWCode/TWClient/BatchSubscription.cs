using System.Runtime.CompilerServices;
using TWClients.JsonRpcMessages;

namespace TWClients;

/// <summary>
///     In the TW server, changes to data can occur as part of transactions (see TW server documentation). The states of
///     the data
///     prior to and following a transaction are the states referenced by the two-state condition of a
///     <see cref="Subscription{T}" />.
///     It is possible (in many applications it will be common) that the condition of a Subscription will be satisfied for
///     multiple
///     tuples of data across a single transaction, and/or that the conditions of <em>multiple</em> Subscriptions will be
///     satisfied
///     across a single transaction.
/// </summary>
/// <remarks>
///     When this occurs and the satisfied subscription conditions come from <em>independently</em> activated Subscription
///     instances,
///     the multiple notifications arrive at the client sequentially in no particular order, and are handled by the client
///     in arrival
///     order. That may be perfectly adequate in many applications. In others, the intermediate states implied by the
///     sequential handling
///     of the notifications can cause problems. In that case, use of a <see cref="BatchSubscription" /> may be useful.
///     A BatchSubscription groups one or more Subscription instances. Over time, new instances may be added to the
///     BatchSubscription
///     and/or existing members may be removed. An already active Subscription may not be added to a BatchSubscription.
///     A BatchSubscription may be activated on any one of a TriggerwareClient's connections.
///     When a BatchSubscription is active, <em>all</em> its members are active (being monitored by the TW server).
///     When a Subscription is removed from an active BatchSubscription, that subscription is deactivated on the TW server.
///     When a transaction in the TW server triggers the condition of any of the Subscriptions in that BatchSubscription, a
///     single
///     notification is sent to the client.
/// </remarks>
/// <example>
///     A typical use of a BatchSubscription consists of sequentially:
///     <list type="number">
///         <item>
///             <description>
///                 Create a new BatchSubscription instance with the constructor
///                 <see cref="BatchSubscription()" />.
///             </description>
///         </item>
///         <item>
///             <description>
///                 Add some Subscriptions to the BatchSubscription with <see cref="AddSubscription(Subscription{T})" />
///                 (alternatively, use the constructor that accepts an initial set of Subscriptions as a parameter).
///             </description>
///         </item>
///         <item>
///             <description>Activate the BatchSubscription with <see cref="Activate" />.</description>
///         </item>
///     </list>
///     To handle the notifications, you can either:
///     <list type="bullet">
///         <item>
///             <description>
///                 Use your own subclass of BatchSubscription, overriding the <see cref="HandleNotification" />
///                 method.
///             </description>
///         </item>
///         <item>
///             <description>
///                 Override the <see cref="Subscription{T}.HandleNotificationFromBatch" /> method of some or all of the
///                 subscriptions in the BatchSubscription.
///             </description>
///         </item>
///     </list>
/// </example>
/// <remarks>
///     The handling of a notification from the BatchSubscription is carried out in the notification handling thread of the
///     connection
///     on which the BatchSubscription was activated. Any division of labor across threads, or attempt to free that thread
///     to handle
///     later-arriving notifications, must be accomplished by code in method overrides.
/// </remarks>
public class BatchSubscription : INotificationInducer
{
    public BatchSubscription()
    {
    }

    public BatchSubscription(IEnumerable<ISubscription> subscriptions)
        : this()
    {
        foreach (var subscription in subscriptions)
            try
            {
                AddSubscription(subscription);
            }
            catch (JsonRpcException e)
            {
            }
    }

    public string? NotificationTag { get; } = TriggerwareClient.NextNotificationMethod("sub");
    public TriggerwareClient? SubscribedOn { get; private set; }
    public HashSet<ISubscription> Subscriptions { get; } = [];


    public Type NotificationType => typeof(BatchNotification);

    public TriggerwareClient? ActiveConnection()
    {
        return SubscribedOn;
    }

    /// <summary>
    ///     add an additional subscription to this BatchSubscription.  Activate the subscription if this BatchSubscription
    ///     is currently active.
    /// </summary>
    /// <param name="subscription"> a subscription to add to this BatchSubscription</param>
    /// <exception cref="Subscription{TRow}.SubscriptionException">
    ///     if the subscription is already part of some other BatchSubscription, or if the
    ///     subscription is already active as an individual subscription
    /// </exception>
    /// <exception cref="JsonRpcException">if the attempt to activate the subscription is rejected by  the TW server.</exception>
    public void AddSubscription(ISubscription subscription)
    {
        var batch = subscription.PartOfBatch;
        if (batch != null)
        {
            if (batch == this) return; //noop, already in this batch.
            throw new Subscription<VoidType>.SubscriptionException(
                "attempt to add a subscription to a second BatchSubscription",
                subscription);
        }

        if (subscription.ActiveConnection() != null)
            throw new Subscription<VoidType>.SubscriptionException(
                "attempt to add an already active subscription to a BatchSubscription",
                subscription);
        Subscriptions.Add(subscription);
        subscription.PartOfBatch = this;
        if (SubscribedOn != null)
            subscription.RegisterWithTw(SubscribedOn);
    }

    public void Activate(TriggerwareClient client)
    {
        if (SubscribedOn != null)
        {
            if (SubscribedOn == client)
                return;

            throw new Subscription<VoidType>.SubscriptionException(
                "attempt to activate a batch subscription on a second connection");
        }

        client.RegisterNotificationInducer(NotificationTag, this);

        foreach (var subscription in Subscriptions)
            subscription.RegisterWithTw(client);
    }

    /// <summary>
    ///     deactivate all the subscriptions in this BatchSubscription. This does not affect the set of Subscriptions
    ///     conatained by the BatchSubscription; it just means that they will not be monitored by the TW server and thus
    ///     no notifications will be sent.  The BatchSubscription may later be activated on the same or a different connection.
    ///     deactivate is a noop if the BatchSubscription is not currently active.
    ///     <para></para>
    ///     Implementation note:  The TW server does not provide a means for deactivating multiple subscriptions 'atomically'.
    ///     When you deactivate a BatchSubscription, its component subscriptions are deactivated sequentially.  This may create
    ///     a race condition with transactions taking place in the server. Those transactions could result in notifications
    ///     being sent that contain only the notifications that arise from some still-active members of the BatchNotification.
    /// </summary>
    /// <exception cref="JsonRpcException">
    ///     JsonRpcException if the deactivation of any of the subscriptions fails for any
    ///     reason
    /// </exception>
    [MethodImpl(MethodImplOptions.Synchronized)]
    public void Deactivate()
    {
        if (SubscribedOn == null) return;
        foreach (var subscription in Subscriptions) subscription.UnregisterWithTw();
        SubscribedOn.UnregisterNotificationInducer(NotificationTag);
        SubscribedOn = null;
    }
    
    /// <param name="batch">the BatchNotification containing a structured collection of individual notifications
    /// from the subscriptsions of this BatchSubscription</param>
    public void HandleNotification(BatchNotification batch) {
        foreach (var subBatch in batch.Notifications) {
            var sub = subBatch.Key;
            var tuples = subBatch.Value;
            sub.HandleBatchNotifications(tuples);
        }		
    }

    public bool Closed => false;

}