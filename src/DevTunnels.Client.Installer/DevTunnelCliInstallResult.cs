namespace DevTunnels.Client.Installer;

/// <summary>Result of a <see cref="IDevTunnelCliInstaller.InstallAsync"/> attempt.</summary>
public sealed record DevTunnelCliInstallResult(
    bool Success,
    string InstallerUsed,
    string? InstalledPath,
    string? StandardOutput,
    string? StandardError,
    string? FailureReason);
