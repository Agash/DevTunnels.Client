namespace DevTunnels.Client;

/// <summary>
/// Represents a failure reported by the underlying <c>devtunnel</c> CLI.
/// </summary>
public sealed class DevTunnelsCliException : Exception
{
    /// <summary>
    /// Initializes a new instance of the <see cref="DevTunnelsCliException" /> class.
    /// </summary>
    public DevTunnelsCliException(string message, DevTunnelCommandResult commandResult, Exception? innerException = null)
        : base(message, innerException) => CommandResult = commandResult;

    /// <summary>
    /// Gets the captured command result.
    /// </summary>
    public DevTunnelCommandResult CommandResult { get; }
}
