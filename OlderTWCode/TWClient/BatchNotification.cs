using TWClients.JsonRpcMessages;

namespace TWClients;

/// <summary>
///     A BatchNotification comprises multiple notifications received in response to a single transition on the TW server.
///     BatchNotification groups notifications from one or more Subscriptions belonging to a single
///     <see cref="BatchSubscription" />.
///     A BatchNotification is supplied as the parameter of a BatchSubscription's
///     <see cref="HandleNotification" /> method.
///     These notifications are represented as a Map, using the Subscription as the key and a collection of tuples as the
///     value
///     for each Map entry.
/// </summary>
public class BatchNotification : Notification
{
    public Dictionary<ISubscription, object> Notifications { get; }

    public override void Handle(JsonRpcClient client, string notificationTag)
    {
        var bsub = (BatchSubscription)((TriggerwareClient)client).NotificationInducers[notificationTag];
        if (bsub == null)
            Logging.Log("internal error: received a batch subscription notification with an unknown tag " +
                        notificationTag);
        else
            try
            {
                bsub.HandleNotification(this);
            }
            catch (Exception e)
            {
                Logging.Log("error handling batch notification" + e);
            }
    }
}