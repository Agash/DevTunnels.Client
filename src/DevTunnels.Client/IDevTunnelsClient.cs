namespace DevTunnels.Client;

/// <summary>
/// Defines the public client surface for interacting with the Azure Dev Tunnels CLI.
/// </summary>
public interface IDevTunnelsClient
{
    /// <summary>
    /// Probes the local machine for a usable <c>devtunnel</c> CLI installation.
    /// </summary>
    ValueTask<DevTunnelCliProbeResult> ProbeCliAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the resolved CLI version.
    /// </summary>
    ValueTask<Version> GetVersionAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the current login status.
    /// </summary>
    ValueTask<DevTunnelLoginStatus> GetLoginStatusAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Ensures that the CLI is logged in, coalescing concurrent callers.
    /// </summary>
    ValueTask<DevTunnelLoginStatus> EnsureLoggedInAsync(LoginProvider? provider = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Starts an interactive login flow for the specified provider.
    /// </summary>
    ValueTask<DevTunnelLoginStatus> LoginAsync(LoginProvider provider, CancellationToken cancellationToken = default);

    /// <summary>
    /// Logs out the current CLI session.
    /// </summary>
    ValueTask LogoutAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates or updates a tunnel and reconciles its tunnel-level anonymous access policy.
    /// </summary>
    ValueTask<DevTunnelStatus> CreateOrUpdateTunnelAsync(string tunnelId, DevTunnelOptions options, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves an existing tunnel.
    /// </summary>
    ValueTask<DevTunnelStatus> GetTunnelAsync(string tunnelId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a tunnel.
    /// </summary>
    ValueTask<DevTunnelDeleteResult> DeleteTunnelAsync(string tunnelId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves the ports attached to a tunnel.
    /// </summary>
    ValueTask<DevTunnelPortList> GetPortListAsync(string tunnelId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates or replaces a tunnel port and optionally reconciles anonymous access.
    /// </summary>
    ValueTask<DevTunnelPortStatus> CreateOrReplacePortAsync(string tunnelId, int portNumber, DevTunnelPortOptions options, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a tunnel port.
    /// </summary>
    ValueTask<DevTunnelPortDeleteResult> DeletePortAsync(string tunnelId, int portNumber, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves access policies for a tunnel or tunnel port.
    /// </summary>
    ValueTask<DevTunnelAccessStatus> GetAccessAsync(string tunnelId, int? portNumber = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Resets access policies for a tunnel or tunnel port.
    /// </summary>
    ValueTask<DevTunnelAccessStatus> ResetAccessAsync(string tunnelId, int? portNumber = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates an access entry for a tunnel or tunnel port.
    /// </summary>
    ValueTask<DevTunnelAccessStatus> CreateAccessAsync(string tunnelId, bool anonymous, bool deny = false, int? portNumber = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes a raw CLI command and returns the captured output.
    /// </summary>
    ValueTask<DevTunnelCommandResult> ExecuteRawAsync(IReadOnlyList<string> arguments, bool useShellExecute = false, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists all tunnels owned by the current logged-in account.
    /// </summary>
    ValueTask<DevTunnelList> ListTunnelsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates an existing tunnel port's description or labels without delete+recreate.
    /// </summary>
    ValueTask<DevTunnelPortStatus> UpdatePortAsync(string tunnelId, int portNumber, DevTunnelPortOptions options, CancellationToken cancellationToken = default);

    /// <summary>
    /// Issues a scoped access token for the specified tunnel.
    /// </summary>
    /// <param name="tunnelId">The tunnel to issue a token for.</param>
    /// <param name="scopes">Token scopes. Defaults to <c>["connect"]</c> when null or empty.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    ValueTask<string> GetAccessTokenAsync(string tunnelId, IReadOnlyList<string>? scopes = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Starts a long-running <c>devtunnel host</c> session.
    /// </summary>
    ValueTask<IDevTunnelHostSession> StartHostSessionAsync(DevTunnelHostStartOptions options, CancellationToken cancellationToken = default);
}
