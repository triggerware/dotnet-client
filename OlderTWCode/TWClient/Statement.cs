using TWClients.JsonRpcMessages;

namespace TWClients;

/// <summary>
/// In this version of the library, only two classes, <see cref="QueryStatement"/> and <see cref="PreparedQuery"/>, implement statement.
/// </summary>
public interface IStatement : IDisposable
{
    public abstract JsonRpcClient Client { get; }
}