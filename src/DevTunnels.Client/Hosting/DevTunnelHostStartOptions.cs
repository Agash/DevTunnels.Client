namespace DevTunnels.Client;

/// <summary>
/// Configures host-session startup for the long-running <c>devtunnel host</c> command.
/// </summary>
public sealed record DevTunnelHostStartOptions
{
    /// <summary>Gets or sets the existing tunnel identifier to host.</summary>
    public string? TunnelId { get; init; }

    /// <summary>Gets or sets the local port to host when starting without a pre-created tunnel identifier.</summary>
    public int? PortNumber { get; init; }

    /// <summary>Gets or sets the working directory for the host process.</summary>
    public string? WorkingDirectory { get; init; }

    /// <summary>Gets or sets an optional timeout for waiting until the host session becomes ready.</summary>
    public TimeSpan ReadyTimeout { get; init; } = TimeSpan.FromSeconds(20);
}
