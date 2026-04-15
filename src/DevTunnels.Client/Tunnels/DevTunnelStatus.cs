using DevTunnels.Client.Ports;

namespace DevTunnels.Client.Tunnels;

/// <summary>
/// Represents a Dev Tunnel returned by the CLI.
/// </summary>
public sealed record DevTunnelStatus
{
    /// <summary>Gets the unique tunnel identifier.</summary>
    public string TunnelId { get; init; } = string.Empty;

    /// <summary>Gets the host connection count reported by the CLI.</summary>
    public int HostConnections { get; init; }

    /// <summary>Gets the client connection count reported by the CLI.</summary>
    public int ClientConnections { get; init; }

    /// <summary>Gets the configured tunnel description.</summary>
    public string Description { get; init; } = string.Empty;

    /// <summary>Gets the configured labels.</summary>
    public IReadOnlyList<string> Labels { get; init; } = [];

    /// <summary>Gets the known ports attached to the tunnel.</summary>
    public IReadOnlyList<DevTunnelPort> Ports { get; init; } = [];
}
