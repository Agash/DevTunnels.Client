namespace DevTunnels.Client.Access;

/// <summary>
/// Represents the access rules currently applied to a tunnel or tunnel port.
/// </summary>
public sealed record DevTunnelAccessStatus
{
    /// <summary>Gets the access control entries returned by the CLI.</summary>
    public IReadOnlyList<DevTunnelAccessEntry> AccessControlEntries { get; init; } = [];
}
