using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;
using TWClients.JsonRpcMessages;

namespace TWClients;

/// <summary>
///     A PolledQuery is a query that will be executed by the TW server multiple times. The first time it is executed,
///     the answer (a set of 'rows') establishes a 'current state' of the query. For each succeeding execution (referred to
///     as <em>polling</em> the query),
///     <ul>
///         <li>
///             The new answer is compared with the current state, and the differences are sent to the triggerware client
///             in a notification containing a <see cref="RowsDelta" /> value.
///         </li>
///         <li>
///             The new answer then becomes the current state to be used for comparison with the result of the next poll of
///             the query.
///         </li>
///     </ul>
///     Like any other query, a PolledQuery has a query string, a language (FOL or SQL), and a namespace.
///     A polling operation may be performed at any time by executing the <see cref="PollAsync" /> method.
///     Some details of reporting and polling can be configured with a <see cref="PolledQueryControlParameters" />
///     value that is supplied to the constructor of a PolledQuery.
///     <para></para>
///     An instantiable subclass of PolledQuery must provide a <see cref="HandleSuccess" /> method to deal with
///     notifications of
///     changes to the current state. There are errors that can occur during a polling operation (timeout, inability to
///     contact
///     a data source). When such an error occurs, the TW Server will send an "error" notification.
///     <para></para>
///     An instantiable subclass of PolledQuery may provide a <see cref="HandleError" /> method to deal with error
///     notifications.
///     <see cref="ScheduledQuery" /> is a subclass of PolledQuery which provides for polling to occur based on a schedule,
///     rather than
///     solely on demand (i.e., solely by virtue of the client submitting a poll request to the TW server). The existence
///     of
///     scheduled queries is the reason that state deltas are sent to the client as notifications, even when a poll is
///     explicitly
///     requested. Scheduled queries may still be polled on demand at any time.
///     Polling may be terminated by <see cref="CloseQuery" />
///     <para></para>
///     If a polling operation is ready to start (whether due to its schedule or an explicit poll request) and a previous
///     poll of
///     the query has not completed, the poll operation that is ready to start is simply skipped, and an error notification
///     is
///     sent to the client.
///     <para></para>
/// </summary>
/// <typeparam name="TRow">The class that represents a single 'row' of the answer to the query.</typeparam>
/// <seealso cref="ScheduledQuery" />
public abstract class PolledQuery<TRow> : AbstractQuery<TRow>
{
    protected readonly PositionalParameterRequest<VoidType> PollRequest = new("poll-now", 1, 2);
    protected readonly PositionalParameterRequest<VoidType> ReleasePolledQueryRequest = new("close-polled-query", 1, 1);

    protected PolledQuery(
        string query,
        string language,
        string schema,
        TriggerwareClient client,
        PolledQuerySchedule schedule,
        PolledQueryControlParameters controls)
        : base(query, language, schema)
    {
        Schedule = schedule;
        Controls = controls;
        Client = client;
        Register();
    }

    protected PolledQuery(
        PreparedQuery<TRow> pq,
        PolledQuerySchedule schedule,
        PolledQueryControlParameters controls)
        : base(pq)
    {
        Schedule = schedule;
        Controls = controls;
        PreparedQuery = pq;
        Client = (TriggerwareClient)pq.Client;

        if (!pq.FullyInstantiated())
            throw new TriggerwareClientException(
                "registering a PolledQuery instance based on a PreparedQuery requires that the PreparedQuery to have values for all of its parameters");
    }

    protected string? NotificationMethod { get; } = TriggerwareClient.NextNotificationMethod("pq");
    public Type?[] SignatureTypes { get; protected set; }
    public new string[] SignatureNames { get; protected set; }
    public string[] SignatureTypeNames { get; protected set; }
    protected PolledQuerySchedule Schedule { get; }
    protected PolledQueryControlParameters? Controls { get; }

    /// <summary>
    ///     True means RegisterAsync was successfully called, and the server has a stored handle for this query.
    /// </summary>
    public bool Registered { get; protected set; }

    /// <summary>
    ///     True means at least one polling operation has succeeded, so the polled query does have a current state.
    ///     This will be false if called before or during the first execution of handleSuccess for this query, but true
    ///     thereafter.
    /// </summary>
    public bool Succeeded { get; protected set; }

    protected PreparedQuery<TRow>? PreparedQuery { get; }

    protected Dictionary<string, object?> Parameters =>
        new()
        {
            { "query", Query },
            { "language", Language },
            { "namespace", Schema },
            { "method", NotificationMethod }
        };

    protected Dictionary<string, object?> ParametersPrepared
        => AddRequestParamsForControls(new Dictionary<string, object?>
        {
            { "preparedQueryHandle", PreparedQuery!.TwHandle },
            { "preparedQueryParameters", PreparedQuery.ParamsByIndex },
            { "method", NotificationMethod },
            { "method", NotificationMethod }
        });

    /// <summary>
    ///     Perform an on-demand poll of this PolledQuery.
    /// </summary>
    /// <exception cref="TriggerwareClientException">
    ///     if this PolledQuery has been closed, or if it has not been registered with the server.
    /// </exception>
    /// <exception cref="JsonRpcRuntimeException">
    ///     for any error (probably communications failure) signalled by the TW server
    ///     This relates <em>only</em> to errors with understanding and acknowledging the request. Any errors that
    ///     occur in carrying out the requested poll operation are encoded in an error notification to be handled
    ///     by the handleError method.
    /// </exception>
    // [MethodImpl(MethodImplOptions.Synchronized)]
    public async Task PollAsync()
    {
        if (Closed) throw new TriggerwareClientException("attempt to poll a closed PollQuery");
        if (TwHandle == null)
            throw new TriggerwareClientException("attempt to pull an unregistered PollQuery");

        if (Client == null)
            throw new TriggerwareClientException("client not initialized");

        await (Controls?.PollTimeout is { } timeout
            ? Client.ExecuteAsync(PollRequest, TwHandle, timeout.Seconds)
            : Client.ExecuteAsync(PollRequest, TwHandle));
    }

    /// <summary>
    ///     Closes the Query:
    ///     <ul>
    ///         <li>
    ///             marks this PolledQuery as closed, so than any future poll requests issued by the client will throw an
    ///             exception
    ///         </li>
    ///         <li>tells the TW Server to release all resources associated with the polled query</li>
    ///         <li>
    ///             if this PolledQuery is a ScheduledQuery, the TW server will not initiate any further poll operations on the
    ///             query
    ///         </li>
    ///     </ul>
    ///     It is possible that the notification queue for the query's connection contains notifications for this query at the
    ///     time closeQuery is invoked.
    ///     The PolledQuery's handleSuccess/handleFailure methods will eventually be invoked for such notifications.
    ///     <para></para>
    ///     It is even possible (due to race conditions) that further notifications will arrive after closeQuery is invoked.
    ///     Such notification will be discarded.
    ///     <para></para>
    ///     closeQuery is a noop for a PolledQuery that is already closed or one
    ///     that has never been registered.  A PolledQuery is implicitly unregistered if the query's connection is closed.
    /// </summary>
    /// <returns>
    ///     true if the query was successfully closed.  false if it was already closed or if the server was unable to confirm
    ///     closing it
    /// </returns>
    [MethodImpl(MethodImplOptions.Synchronized)]
    public override bool CloseQuery()
    {
        if (Client == null || TwHandle == null)
            throw new TriggerwareClientException("client not initialized");

        if (Closed) return false;
        try
        {
            Client.Execute(ReleasePolledQueryRequest, TwHandle);
            Closed = true;
            return true;
        }
        catch (Exception e)
        {
            Logging.Log("Error closing a polled query: " + e.Message);
            return false;
        }
    }


    private Dictionary<string, object?> AddRequestParamsForControls(Dictionary<string, object?> parameters)
    {
        if (Controls != null)
        {
            if (Controls.PollTimeout != null) parameters["timelimit"] = Controls.PollTimeout;
            if (Controls.ReportUnchanged) parameters["report-noops"] = true;
            parameters["report-initial"] = Controls.ReportInitial ? "with delta" : "without delta";
        }
        else
        {
            parameters["report-inital"] = "without delta";
        }

        return parameters;
    }

    /// <summary>
    ///     If not overridden, error notifications will be logged but otherwise ignored.
    /// </summary>
    /// <param name="message">text explaining the failure.</param>
    /// <param name="ts">
    ///     Polling time when the failure occured. Timestamp is generated from the server, so comparing two
    ///     timestamps not synchronized to the server's clock will not work as expected.
    /// </param>
    public virtual void HandleError(string message, DateTime ts)
    {
        Logging.Log("error notification from polled query " + this + " polled at " + ts + ": " + message);
    }

    /// <summary>
    ///     If not overridden, the log will log something simple.
    /// </summary>
    /// <param name="delta"> the changes detected by a polling operation</param>
    /// <param name="ts">
    ///     Polling time when the polling operation was done. Timestamp is generated from the server, so comparing two
    ///     timestamps
    ///     not synchronized to the server's clock will not work as expected.
    /// </param>
    public virtual void HandleSuccess(RowsDelta<TRow> delta, DateTime ts)
    {
        Logging.Log("Poll " + this + " succeeded at " + ts + ", delta " + delta);
    }

    [MethodImpl(MethodImplOptions.Synchronized)]
    protected void Register()
    {
        if (Registered) return;

        var pqResult =
            Client!.Rpc.InvokeAsync<PolledQueryRegistration>("create-polled-query",
                PreparedQuery == null ? ParametersPrepared : Parameters).Result;

        TwHandle = pqResult.Handle;
        SignatureTypes = pqResult.TypeSignature;
        SignatureTypeNames = pqResult.TypeNames;
        SignatureNames = pqResult.AttributeNames;
        Registered = true;
    }

    /// <summary>
    ///     perform an on-demand poll of this PolledQuery.
    /// </summary>
    /// <exception cref="TriggerwareClientException">if this PolledQuery has been closed or has never been registered.</exception>
    /// <exception cref="JsonRpcException">
    ///     for any error (probably communications failure) signalled by the TW server
    ///     This relates <em>only</em> to errors with understanding and acknowledging the request. Any errors that
    ///     occur in carrying out the requested poll operation are encoded in an error notification to be handled
    ///     by the handleError method.
    /// </exception>
    [MethodImpl(MethodImplOptions.Synchronized)]
    public void Poll()
    {
        if (Closed)
            throw new TriggerwareClientException("attempt to poll a closed PolledQuery");
        if (TwHandle == null)
            throw new TriggerwareClientException("attempt to poll an unregistered PolledQuery");
        var timeout = Controls?.PollTimeout;
        if (timeout != null)
            Client?.Execute(PollRequest, TwHandle, timeout.Value.Seconds);
        else
            Client?.Execute(PollRequest, TwHandle);
    }

    public override object Clone()
    {
        var clone = MemberwiseClone();
        return clone;
    }

    public class PolledQueryControlParameters(bool reportUnchanged, TimeSpan? pollTimeout, bool reportInitial)
    {
        public PolledQueryControlParameters() : this(false, null, false)
        {
        }

        /// <summary>
        ///     Determines whether a success notification is sent to the TriggerwareClient when a poll of the query
        ///     returns the same set of values as the current state.  The delta value for such a notification would have empty
        ///     sets of rows for both the added and deleted sets.
        ///     The default is false, meaning <em>not</em> to send such notifications.		 *
        /// </summary>
        public bool ReportUnchanged => reportUnchanged;

        /// <summary>
        ///     A limit on how much time should be allowed for a polling operation.
        ///     If the poll exceeds this time limit, it is effectively aborted (on the tw server) and
        ///     an error notification is sent to the TriggerwareClient.
        ///     The default is null, meaning that no time limit is used.
        /// </summary>
        public TimeSpan? PollTimeout => pollTimeout;

        /// <summary>
        ///     Determines whether the success notification sent when the first successful poll operation completes
        ///     will contain the row values that constitute the initial state.
        ///     Such a notification will always have an empty set of 'deleted' rows.  The only means to reliably distinguish
        ///     this from other success notifications is the Succeeded getter.
        ///     The default value is false,  meaning <em>not</em> to include rows in that notification.
        /// </summary>
        public bool ReportInitial => reportInitial;
    }


    [method: JsonConstructor]
    public class PolledQueryRegistration(int handle, SignatureElement[] signature)
    {
        public int Handle => handle;
        public SignatureElement[] Signature => signature;

        [JsonIgnore] public Type?[] TypeSignature => TypeSignatureTypes(Signature);
        [JsonIgnore] public string[] TypeNames => TypeSignatureTypeNames(Signature);
        [JsonIgnore] public string?[] AttributeNames => SignatureNames(Signature);
    }
}