namespace Triggerware;

public interface ILogger
{
    /// <summary>
    /// Logs a message.
    /// </summary>
    /// <param name="message">A message to be logged.</param>
    void Log(string message)
    {
    }

    /// <summary>
    /// Logs a message with an exception.
    /// </summary>
    /// <param name="e">An exception that was caught, leading to this logging request.</param>
    /// <param name="message">A string indicating a problem that was encountered.</param>
    void Log(Exception e, string message)
    {
    }

    /// <summary>
    /// Logs a formatted message.
    /// </summary>
    /// <param name="format">A format string similar to C#'s string.Format method.</param>
    /// <param name="args">Arguments to be consumed by the format string.</param>
    void Log(string format, params object[] args)
    {
    }
}

internal class EmptyLogger : ILogger
{
}

/// <summary>
/// Logging provides static methods that let an application using this library
/// control the location and content of output produced by the logging statements within the library.
/// A future version of this library will remove this class and use one of the standard .NET loggers for its logging.
/// </summary>
public class Logging
{
    /// <summary>
    /// Logs a message.
    /// </summary>
    /// <param name="message">A message to be logged.</param>
    public static void Log(string message)
    {
        Logger.Log(message);
    }

    /// <summary>
    /// Logs a message with an exception.
    /// </summary>
    /// <param name="e">An exception that was caught, leading to this logging request.</param>
    /// <param name="message">A string indicating a problem that was encountered.</param>
    public static void Log(Exception e, string message)
    {
        Logger.Log(e, message);
    }

    /// <summary>
    /// Logs a formatted message.
    /// </summary>
    /// <param name="format">A format string similar to C#'s string.Format method.</param>
    /// <param name="args">Arguments to be consumed by the format string.</param>
    public static void Log(string format, params object[] args)
    {
        Logger.Log(format, args);
    }

    /// <summary>
    /// Gets an ILogger instance that produces no logging output.
    /// </summary>
    public static ILogger EmptyLogger { get; } = new EmptyLogger();

    /// <summary>
    /// Represents the ILogger implementation currently being used by this library. 
    /// Initially, this is the empty logger.
    /// </summary>
    public static ILogger Logger { get; set; } = EmptyLogger;
}