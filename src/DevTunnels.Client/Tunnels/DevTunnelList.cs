namespace DevTunnels.Client.Tunnels;

/// <summary>
/// Represents the list of tunnels returned by <c>devtunnel list</c>.
/// </summary>
public sealed record DevTunnelList
{
    /// <summary>Gets the tunnels owned by the current account.</summary>
    public IReadOnlyList<DevTunnelStatus> Tunnels { get; init; } = [];
}
