namespace DevTunnels.Client;

/// <summary>
/// Represents a failure reported by the underlying <c>devtunnel</c> CLI.
/// </summary>
public sealed class DevTunnelsCliException(
    string message,
    DevTunnelCommandResult commandResult,
    Exception? innerException = null) : Exception(message, innerException)
{
    /// <summary>
    /// Gets the captured command result.
    /// </summary>
    public DevTunnelCommandResult CommandResult { get; } = commandResult;
}
