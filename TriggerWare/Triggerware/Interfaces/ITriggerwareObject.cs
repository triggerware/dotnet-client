namespace Triggerware.Interfaces;

public interface ITriggerwareObject
{
    /// <summary>
    /// The <see cref="TriggerwareClient"/> this object is associated with.
    /// </summary>
    public TriggerwareClient Client { get; }
    
    /// <summary>
    /// The handle of this object on the server. Null if not registered.
    /// </summary>
    public long? Handle { get; }
}