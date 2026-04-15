namespace DevTunnels.Client.Hosting;

/// <summary>
/// Represents a long-running <c>devtunnel host</c> process managed by the client library.
/// </summary>
public interface IDevTunnelHostSession : IAsyncDisposable
{
    /// <summary>
    /// Gets the current lifecycle state.
    /// </summary>
    DevTunnelHostState State { get; }

    /// <summary>
    /// Gets the tunnel identifier once known.
    /// </summary>
    string? TunnelId { get; }

    /// <summary>
    /// Gets the public URL once known.
    /// </summary>
    Uri? PublicUrl { get; }

    /// <summary>
    /// Gets the most recent output line observed from the host process.
    /// </summary>
    string? LastOutputLine { get; }

    /// <summary>
    /// Gets the failure reason when the process exits unexpectedly.
    /// </summary>
    string? FailureReason { get; }

    /// <summary>
    /// Raised whenever the host process emits an output line.
    /// </summary>
    event EventHandler<DevTunnelHostOutputReceivedEventArgs>? OutputReceived;

    /// <summary>
    /// Waits until the host session is ready or fails.
    /// </summary>
    /// <param name="cancellationToken">Cancels the wait.</param>
    ValueTask WaitForReadyAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Waits for the host process to exit.
    /// </summary>
    /// <param name="cancellationToken">Cancels the wait.</param>
    ValueTask WaitForExitAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Stops the host process.
    /// </summary>
    /// <param name="cancellationToken">Cancels the stop operation.</param>
    ValueTask StopAsync(CancellationToken cancellationToken = default);
}
