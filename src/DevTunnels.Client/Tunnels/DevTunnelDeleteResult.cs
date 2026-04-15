namespace DevTunnels.Client;

/// <summary>
/// Represents the result of deleting a tunnel.
/// </summary>
public sealed record DevTunnelDeleteResult
{
    /// <summary>Gets the deleted tunnel identifier returned by the CLI.</summary>
    public string DeletedTunnel { get; init; } = string.Empty;
}
