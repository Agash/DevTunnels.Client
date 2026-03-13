namespace DevTunnels.Client;

/// <summary>
/// Typed login status returned by the Azure Dev Tunnels CLI.
/// </summary>
public sealed record DevTunnelLoginStatus
{
    /// <summary>
    /// Gets the textual status returned by the CLI.
    /// </summary>
    public string Status { get; init; } = string.Empty;

    /// <summary>
    /// Gets the provider currently associated with the login, if any.
    /// </summary>
    public LoginProvider? Provider { get; init; }

    /// <summary>
    /// Gets the username or account label returned by the CLI.
    /// </summary>
    public string? Username { get; init; }

    /// <summary>
    /// Gets a value indicating whether the CLI reports a logged-in state.
    /// </summary>
    public bool IsLoggedIn => string.Equals(Status, "Logged in", StringComparison.OrdinalIgnoreCase);
}

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

/// <summary>
/// Represents the list of ports attached to a tunnel.
/// </summary>
public sealed record DevTunnelPortList
{
    /// <summary>Gets the returned ports.</summary>
    public IReadOnlyList<DevTunnelPort> Ports { get; init; } = [];
}

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

/// <summary>
/// Represents the result of deleting a tunnel port.
/// </summary>
public sealed record DevTunnelPortDeleteResult
{
    /// <summary>Gets the deleted port number or identifier string returned by the CLI.</summary>
    public string DeletedPort { get; init; } = string.Empty;
}

/// <summary>
/// Represents the result of deleting a tunnel.
/// </summary>
public sealed record DevTunnelDeleteResult
{
    /// <summary>Gets the deleted tunnel identifier returned by the CLI.</summary>
    public string DeletedTunnel { get; init; } = string.Empty;
}

/// <summary>
/// Represents the access rules currently applied to a tunnel or tunnel port.
/// </summary>
public sealed record DevTunnelAccessStatus
{
    /// <summary>Gets the access control entries returned by the CLI.</summary>
    public IReadOnlyList<DevTunnelAccessEntry> AccessControlEntries { get; init; } = [];
}

/// <summary>
/// Represents a single access control entry returned by the CLI.
/// </summary>
public sealed record DevTunnelAccessEntry
{
    /// <summary>Gets the entry type, such as <c>Anonymous</c>.</summary>
    public string Type { get; init; } = string.Empty;

    /// <summary>Gets a value indicating whether the rule is a deny rule.</summary>
    public bool IsDeny { get; init; }

    /// <summary>Gets a value indicating whether the rule is inherited.</summary>
    public bool IsInherited { get; init; }

    /// <summary>Gets the subjects associated with the rule.</summary>
    public IReadOnlyList<string> Subjects { get; init; } = [];

    /// <summary>Gets the scopes associated with the rule.</summary>
    public IReadOnlyList<string> Scopes { get; init; } = [];
}

/// <summary>
/// Represents the list of tunnels returned by <c>devtunnel list</c>.
/// </summary>
public sealed record DevTunnelList
{
    /// <summary>Gets the tunnels owned by the current account.</summary>
    public IReadOnlyList<DevTunnelStatus> Tunnels { get; init; } = [];
}

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

/// <summary>
/// Represents the lifecycle state of a running host session.
/// </summary>
public enum DevTunnelHostState
{
    /// <summary>The host process has been created but is not ready yet.</summary>
    Starting,

    /// <summary>The host process is running and produced a usable public URL or tunnel identity.</summary>
    Running,

    /// <summary>The host process has stopped cleanly.</summary>
    Stopped,

    /// <summary>The host process failed or exited unexpectedly.</summary>
    Failed
}

/// <summary>
/// Event data for output produced by a running host session.
/// </summary>
/// <param name="IsError">Whether the line originated from standard error.</param>
/// <param name="Line">The output line.</param>
public sealed record DevTunnelHostOutputReceivedEventArgs(bool IsError, string Line);
