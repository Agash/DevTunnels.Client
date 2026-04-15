namespace DevTunnels.Client;

/// <summary>
/// Represents the result of deleting a tunnel port.
/// </summary>
public sealed record DevTunnelPortDeleteResult
{
    /// <summary>Gets the deleted port number or identifier string returned by the CLI.</summary>
    public string DeletedPort { get; init; } = string.Empty;
}
