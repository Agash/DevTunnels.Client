namespace DevTunnels.Client.Installer;

/// <summary>Installs the devtunnel CLI using the platform-appropriate package manager.</summary>
public interface IDevTunnelCliInstaller
{
    /// <summary>Detects which installer is available on the current platform.</summary>
    /// <returns>The installer name (e.g. "winget", "brew", "curl", "wget"),
    /// or <see langword="null"/> if none is available.</returns>
    ValueTask<string?> DetectInstallerAsync(CancellationToken cancellationToken = default);

    /// <summary>Installs the devtunnel CLI using the detected or configured installer.</summary>
    ValueTask<DevTunnelCliInstallResult> InstallAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Uninstalls the devtunnel CLI using the platform-appropriate method.
    /// </summary>
    /// <remarks>
    /// On Windows this uses <c>winget uninstall</c>; on macOS <c>brew uninstall</c>;
    /// on Linux the installation directory (<c>~/.devtunnel</c>) is removed via
    /// <c>rm -rf</c>. Shell profile PATH entries added by the Linux installer script
    /// are not removed automatically.
    /// </remarks>
    ValueTask<DevTunnelCliUninstallResult> UninstallAsync(CancellationToken cancellationToken = default);
}
