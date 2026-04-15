namespace DevTunnels.Client;

/// <summary>
/// Represents the list of ports attached to a tunnel.
/// </summary>
public sealed record DevTunnelPortList
{
    /// <summary>Gets the returned ports.</summary>
    public IReadOnlyList<DevTunnelPort> Ports { get; init; } = [];
}
