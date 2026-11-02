namespace DevTunnels.Client.Hosting;

/// <summary>
/// How a hosted tunnel stands, as a <see cref="DevTunnelHostMonitor"/> last observed it.
/// </summary>
public enum DevTunnelHostConnectivity
{
    /// <summary>The host has not connected yet.</summary>
    Starting,

    /// <summary>This host is connected to the relay and serving.</summary>
    Connected,

    /// <summary>This host lost the relay. The CLI reconnects by itself, so this can recover.</summary>
    Disconnected,

    /// <summary>The host process ended.</summary>
    Stopped,
}

/// <summary>
/// One observation of a hosted tunnel.
/// </summary>
public sealed record DevTunnelHostStatus
{
    /// <summary>Gets how this host stands.</summary>
    public DevTunnelHostConnectivity Connectivity { get; init; }

    /// <summary>Gets the tunnel ID, cluster-qualified once the service or the CLI has reported it.</summary>
    public string? TunnelId { get; init; }

    /// <summary>Gets the public URL of each hosted port.</summary>
    public IReadOnlyDictionary<int, Uri> Ports { get; init; } = new Dictionary<int, Uri>();

    /// <summary>Gets how many hosts the service counts on the tunnel, or null before it has been asked.</summary>
    public int? HostConnections { get; init; }

    /// <summary>
    /// Gets whether another host holds the tunnel: this one is disconnected while the service still counts one.
    /// The count covers every machine on the account, so it never proves this host is connected.
    /// </summary>
    public bool IsTakenOver { get; init; }

    /// <summary>Gets the last error from asking the service, or null.</summary>
    public string? Error { get; init; }

    /// <summary>Gets when this was observed.</summary>
    public DateTimeOffset ObservedAt { get; init; }
}

/// <summary>
/// Carries a change of a hosted tunnel's status.
/// </summary>
/// <param name="previous">The status before.</param>
/// <param name="current">The status now.</param>
public sealed class DevTunnelHostStatusChangedEventArgs(
    DevTunnelHostStatus previous,
    DevTunnelHostStatus current
) : EventArgs
{
    /// <summary>Gets the status before.</summary>
    public DevTunnelHostStatus Previous { get; } = previous;

    /// <summary>Gets the status now.</summary>
    public DevTunnelHostStatus Current { get; } = current;
}

/// <summary>
/// Options for a <see cref="DevTunnelHostMonitor"/>.
/// </summary>
public sealed record DevTunnelHostMonitorOptions
{
    /// <summary>Gets how long to wait for the host's output before asking the service instead.</summary>
    public TimeSpan OutputTimeout { get; init; } = TimeSpan.FromSeconds(5);

    /// <summary>Gets how often to ask the service about the tunnel.</summary>
    public TimeSpan ReconcileInterval { get; init; } = TimeSpan.FromSeconds(30);
}
