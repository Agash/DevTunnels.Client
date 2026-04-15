namespace DevTunnels.Client.Ports;

/// <summary>
/// Represents the status returned after creating or updating a tunnel port.
/// </summary>
public sealed record DevTunnelPortStatus
{
    /// <summary>Gets the tunnel identifier that owns the port.</summary>
    public string TunnelId { get; init; } = string.Empty;

    /// <summary>Gets the port number.</summary>
    public int PortNumber { get; init; }

    /// <summary>Gets the configured protocol.</summary>
    public string Protocol { get; init; } = string.Empty;

    /// <summary>Gets the client connection count.</summary>
    public int ClientConnections { get; init; }
}
