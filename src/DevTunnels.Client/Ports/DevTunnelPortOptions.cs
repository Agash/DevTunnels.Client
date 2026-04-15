namespace DevTunnels.Client.Ports;

/// <summary>
/// Configures tunnel port create or update operations.
/// </summary>
public sealed record DevTunnelPortOptions
{
    /// <summary>Gets or sets the protocol, such as <c>https</c>.</summary>
    public string? Protocol { get; init; }

    /// <summary>Gets or sets an optional description.</summary>
    public string? Description { get; init; }

    /// <summary>Gets or sets the labels to apply to the port.</summary>
    public IReadOnlyList<string> Labels { get; init; } = [];

    /// <summary>Gets or sets an optional anonymous access policy for the port.</summary>
    public bool? AllowAnonymous { get; init; }
}
