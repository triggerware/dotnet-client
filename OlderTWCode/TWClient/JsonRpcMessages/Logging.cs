namespace TWClients.JsonRpcMessages;

public interface ILogger
{
    /// <param name="message">A message to be logged.</param>
    public void Log(string message)
    {
    }

    /// <param name="e">An exception that was caught, leading to this request.</param>
    /// <param name="message">A string indicating a problem that was encountered.</param>
    public void Log(Exception e, string message)
    {
    }
}

public class EmptyLogger : ILogger
{
    public void Log(string message)
    {
    }

    public void Log(Exception e, string message)
    {
    }
}

/// <summary>
///     Logging provides static methods that let an application using this library
///     control the location and content of output produced by the logging statements within the library.
///     <p />
///     A future version of this library will remove this class and use  one of the standard java configurable loggers for
///     its logging.
/// </summary>
public static class Logging
{
    public static ILogger EmptyLogger { get; } = new EmptyLogger();
    public static ILogger Logger { get; set; } = EmptyLogger;

    public static void Log(Exception e, string message)
    {
        Logger.Log(e, message);
    }

    public static void Log(string message)
    {
        Logger.Log(message);
    }
}