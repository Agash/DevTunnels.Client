namespace DevTunnels.Client.Installer;

/// <summary>Options that control <see cref="DevTunnelCliInstaller"/> behaviour.</summary>
public sealed class DevTunnelCliInstallerOptions
{
    /// <summary>Preferred installer override. <see langword="null"/> means auto-detect.</summary>
    public string? PreferredInstaller { get; set; }

    /// <summary>Timeout for installation commands. Defaults to 5 minutes.</summary>
    public TimeSpan InstallTimeout { get; set; } = TimeSpan.FromMinutes(5);
}
