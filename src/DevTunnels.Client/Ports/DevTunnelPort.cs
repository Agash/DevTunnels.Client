namespace DevTunnels.Client.Ports;

/// <summary>
/// Represents a single Dev Tunnel port.
/// </summary>
public sealed record DevTunnelPort
{
    /// <summary>Gets the port number.</summary>
    public int PortNumber { get; init; }

    /// <summary>Gets the configured protocol.</summary>
    public string Protocol { get; init; } = string.Empty;

    /// <summary>Gets the public URI for the port, when available.</summary>
    public Uri? PortUri { get; init; }

    /// <summary>Gets the client connection count, when reported.</summary>
    public int? ClientConnections { get; init; }
}
