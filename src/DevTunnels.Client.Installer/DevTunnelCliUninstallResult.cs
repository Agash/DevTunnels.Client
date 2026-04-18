namespace DevTunnels.Client.Installer;

/// <summary>Result of a <see cref="IDevTunnelCliInstaller.UninstallAsync"/> attempt.</summary>
public sealed record DevTunnelCliUninstallResult(
    bool Success,
    string UninstallerUsed,
    string? StandardOutput,
    string? StandardError,
    string? FailureReason);
