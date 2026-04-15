namespace DevTunnels.Client.Authentication;

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
