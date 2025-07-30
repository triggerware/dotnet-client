using Triggerware.Interfaces;
using Triggerware.JsonStructs;

namespace Triggerware;

/// <summary>
///     A PolledQuery is a query that will be executed by the TW server on a set schedule. As soon as a polled query is
///     created, it is executed by the server and the response (a set of 'rows') establishes a 'current state' of the
///     query. For each succeeding execution (referred to as <em>polling</em> the query),
///     <list type="bullet">
///         <item>
///             <description>
///                 The new answer is compared with the current state, and the differences are sent to the triggerware
///                 client in a notification containing a <see cref="JsonStructs.RowsDelta{T}" /> value.
///             </description>
///         </item>
///         <item>
///             <description>
///                 The new answer then becomes the current state to be used for comparison with the result of the next
///                 poll of the query.
///             </description>
///         </item>
///     </list>
///     Like any other query, a PolledQuery has a query string, a language (FOL or SQL), and a namespace.
///     <para>
///     </para>
///     A polling operation may be performed at any time by executing the <see cref="Poll" /> method.
///     Some details of reporting and polling can be configured with a <see cref="PolledQueryControlParameters" />
///     value that is supplied to the constructor of a PolledQuery.
///     <para>
///     </para>
///     An instantiable subclass of PolledQuery must provide a <see cref="HandleNotification" /> method to deal with
///     notifications of
///     changes to the current state. There are errors that can occur during a polling operation (timeout, inability to
///     contact a data source). When such an error occurs, the TW Server will send an "error" notification.
///     An instantiable subclass of PolledQuery may provide a <see cref="HandleError" /> method to deal with error
///     notifications.
///     <para>
///     </para>
///     Polling may be terminated when <see cref="Dispose" /> is called.
///     <para>
///     </para>
///     If a polling operation is ready to start (whether due to its schedule or an explicit poll request) and a
///     previous poll of the query has not completed, the poll operation that is ready to start is simply
///     skipped, and an error notification is sent to the client.
///     <para>
///     </para>
/// </summary>
/// <inheritdoc cref="AbstractQuery{T}" />
public abstract class PolledQuery<T> : AbstractQuery<T>, IDisposable
{
    private readonly object _lock = new();

    /// <inheritdoc />
    /// <param name="controls">Configures how a polled query's data is sent back.</param>
    /// <param name="schedule">
    ///     The schedule this polled query will run on. See <see cref="PolledQuerySchedule" /> for more
    ///     info.
    /// </param>
    // ReSharper disable InvalidXmlDocComment
    public PolledQuery(
        TriggerwareClient client,
        IQuery query,
        QueryRestriction? restriction = null,
        PolledQueryControlParameters? controls = null,
        PolledQuerySchedule? schedule = null
    ) : base(client, query, restriction)
    {
        MethodName = Client.RegisterPolledQuery();
        BaseParameters["method"] = MethodName;
        if (schedule != null) BaseParameters["schedule"] = schedule;
        if (controls != null)
        {
            BaseParameters["report-initial"] = controls.ReportInitial;
            BaseParameters["report-unchanged"] = controls.ReportUnchanged;
            BaseParameters["delay-schedule"] = controls.Delay;
        }

        var registration = Client.Call<PolledQueryRegistration>("create-polled-query", BaseParameters);
        Handle = (long?)registration.Handle;
        Client.AddMethod(MethodName, HandleNotification);
    }

    /// <summary>
    ///     The name of the method the TW server calls to notify when polling occurs.
    /// </summary>
    public string MethodName { get; }

    /// <summary>
    ///     Whether this polled query has been closed.
    /// </summary>
    public bool IsDisposed { get; private set; }

    /// <summary>
    ///     Closes this polled query.
    /// <para></para>
    ///     It is possible that the notification queue for the query's connection contains notifications for this query at the
    ///     time closeQuery is invoked.
    ///     The PolledQuery's handleSuccess/handleFailure methods will eventually be invoked for such notifications.
    /// <para></para>
    ///     It is even possible (due to race conditions) that further notifications will arrive after closeQuery is invoked.
    ///     Such notification will be discarded.
    /// </summary>
    public void Dispose()
    {
        IsDisposed = true;
        lock (_lock)
        {
            Client.Call<object?>("close-polled-query", [Handle]);
            GC.SuppressFinalize(this);
        }
    }

    /// <summary>
    ///     Perform an on-demand poll of this query (temporarily disregarding the set schedule).
    /// </summary>
    public void Poll()
    {
        lock (_lock)
        {
            var parameters = new Dictionary<string, object>
            {
                { "handle", Handle! }
            };
            if (Timeout.HasValue) parameters["timeout"] = Timeout.Value;

            Client.Call<object?>("poll-now", parameters);
        }
    }

    /// <summary>
    ///     Override this method to handle the polled query's changes in data. The polled query's schedule determines
    ///     when this method will be called.
    /// </summary>
    /// <param name="delta">The change in data since the last poll.</param>
    public abstract void HandleNotification(RowsDelta<T> delta);
}