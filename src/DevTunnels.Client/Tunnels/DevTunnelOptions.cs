namespace DevTunnels.Client;

/// <summary>
/// Configures tunnel create or update operations.
/// </summary>
public sealed record DevTunnelOptions
{
    /// <summary>Gets or sets an optional description.</summary>
    public string? Description { get; init; }

    /// <summary>Gets or sets whether anonymous access should be allowed at the tunnel level.</summary>
    public bool AllowAnonymous { get; init; }

    /// <summary>Gets or sets the tunnel labels to apply.</summary>
    public IReadOnlyList<string> Labels { get; init; } = [];
}
