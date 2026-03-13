namespace DevTunnels.Client;

/// <summary>
/// Configures CLI discovery, retry behavior, and login defaults for <see cref="DevTunnelsClient" />.
/// </summary>
public sealed class DevTunnelsClientOptions
{
    /// <summary>
    /// Gets or sets an explicit path or command name for the <c>devtunnel</c> executable.
    /// </summary>
    public string? CliPathOverride { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether well-known install paths should be searched.
    /// </summary>
    public bool IncludeKnownInstallLocations { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether plain PATH lookup should be attempted.
    /// </summary>
    public bool IncludePathLookup { get; set; } = true;

    /// <summary>
    /// Gets or sets the default timeout applied to one-shot CLI commands.
    /// </summary>
    public TimeSpan CommandTimeout { get; set; } = TimeSpan.FromSeconds(10);

    /// <summary>
    /// Gets or sets the minimum supported CLI version.
    /// </summary>
    public Version MinimumSupportedVersion { get; set; } = new(1, 0, 1435);

    /// <summary>
    /// Gets or sets the preferred interactive login provider when callers do not specify one explicitly.
    /// </summary>
    public LoginProvider PreferredLoginProvider { get; set; } = LoginProvider.Microsoft;

    /// <summary>
    /// Gets or sets the maximum number of CLI attempts for retryable operations.
    /// </summary>
    public int MaxCliAttempts { get; set; } = 3;

    /// <summary>
    /// Gets or sets the delay between retryable CLI attempts.
    /// </summary>
    public TimeSpan CliRetryDelay { get; set; } = TimeSpan.FromSeconds(2);
}
