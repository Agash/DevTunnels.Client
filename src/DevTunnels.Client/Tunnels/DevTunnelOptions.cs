namespace DevTunnels.Client.Tunnels;

/// <summary>
/// Configures tunnel create or update operations.
/// </summary>
public sealed record DevTunnelOptions
{
    /// <summary>Gets or sets an optional description.</summary>
    public string? Description { get; init; }

    /// <summary>Gets or sets whether anonymous access should be allowed at the tunnel level.</summary>
    public bool AllowAnonymous { get; init; }

    /// <summary>
    /// Gets how many idle hours the service keeps the tunnel, between 1 and 720; each host connection renews
    /// it. Null leaves the service's value, and an existing tunnel's, alone.
    /// </summary>
    public int? ExpirationHours { get; init; }

    /// <summary>Gets or sets the tunnel labels to apply.</summary>
    public IReadOnlyList<string> Labels { get; init; } = [];
}
