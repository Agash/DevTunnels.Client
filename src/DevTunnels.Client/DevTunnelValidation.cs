using System.Text.RegularExpressions;

namespace DevTunnels.Client;

/// <summary>
/// Provides validation helpers for Azure Dev Tunnels identifiers.
/// </summary>
public static partial class DevTunnelValidation
{
    /// <summary>
    /// Validates a tunnel identifier against the Azure Dev Tunnels format.
    /// </summary>
    public static bool IsValidTunnelId(string? tunnelId) =>
        !string.IsNullOrWhiteSpace(tunnelId) && TunnelIdRegex().IsMatch(tunnelId);

    /// <summary>
    /// Validates a label against the Azure Dev Tunnels format.
    /// </summary>
    public static bool IsValidLabel(string? label) =>
        !string.IsNullOrWhiteSpace(label) && LabelRegex().IsMatch(label);

    [GeneratedRegex("^[a-z0-9][a-z0-9-]{1,58}[a-z0-9]$")]
    private static partial Regex TunnelIdRegex();

    [GeneratedRegex(@"^[\w\-=_]{1,50}$")]
    private static partial Regex LabelRegex();
}
