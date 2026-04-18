using DevTunnels.Client.Installer.Internal;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace DevTunnels.Client.Installer;

/// <summary>
/// Installs the devtunnel CLI using the platform-appropriate package manager.
/// </summary>
public sealed class DevTunnelCliInstaller : IDevTunnelCliInstaller
{
    private readonly DevTunnelCliInstallerOptions _options;
    private readonly ILogger<DevTunnelCliInstaller> _logger;
    private readonly IInstallerProcessExecutor _processExecutor;
    private readonly IPlatformDetector _platformDetector;

    /// <summary>
    /// Initializes a new instance of the <see cref="DevTunnelCliInstaller"/> class.
    /// </summary>
    /// <param name="options">Optional installer options.</param>
    /// <param name="logger">Optional logger.</param>
    public DevTunnelCliInstaller(DevTunnelCliInstallerOptions? options = null, ILogger<DevTunnelCliInstaller>? logger = null)
        : this(
            options ?? new DevTunnelCliInstallerOptions(),
            logger,
            new SystemInstallerProcessExecutor(),
            new RuntimePlatformDetector())
    {
    }

    internal DevTunnelCliInstaller(
        DevTunnelCliInstallerOptions options,
        ILogger<DevTunnelCliInstaller>? logger,
        IInstallerProcessExecutor processExecutor,
        IPlatformDetector platformDetector)
    {
        _options = options;
        _logger = logger ?? NullLogger<DevTunnelCliInstaller>.Instance;
        _processExecutor = processExecutor;
        _platformDetector = platformDetector;
    }

    // winget exit code 20 (APPINSTALLER_CLI_ERROR_NO_APPLICATIONS_FOUND): the package is not
    // installed. For uninstall this is a success — the desired postcondition is already met.
    private const int WingetNoPackageFoundExitCode = 20;

    /// <inheritdoc />
    public async ValueTask<string?> DetectInstallerAsync(CancellationToken cancellationToken = default)
    {
        if (_platformDetector.IsWindows())
        {
            return await ProbeAsync("winget", ["--version"], cancellationToken).ConfigureAwait(false)
                ? "winget"
                : null;
        }

        if (_platformDetector.IsMacOS())
        {
            return await ProbeAsync("brew", ["--version"], cancellationToken).ConfigureAwait(false)
                ? "brew"
                : null;
        }

        // Linux (else branch)
        if (await ProbeAsync("curl", ["--version"], cancellationToken).ConfigureAwait(false))
        {
            return "curl";
        }

        if (await ProbeAsync("wget", ["--version"], cancellationToken).ConfigureAwait(false))
        {
            return "wget";
        }

        return null;
    }

    /// <inheritdoc />
    public async ValueTask<DevTunnelCliInstallResult> InstallAsync(CancellationToken cancellationToken = default)
    {
        string? installer = _options.PreferredInstaller;

        if (installer is null)
        {
            installer = await DetectInstallerAsync(cancellationToken).ConfigureAwait(false);
        }

        if (installer is null)
        {
            string failureReason = GetNoInstallerFailureReason();
            _logger.LogWarning("No supported installer detected. {FailureReason}", failureReason);
            return new DevTunnelCliInstallResult(
                Success: false,
                InstallerUsed: string.Empty,
                InstalledPath: null,
                StandardOutput: null,
                StandardError: null,
                FailureReason: failureReason);
        }

        _logger.LogInformation("Installing devtunnel CLI using '{Installer}'.", installer);

        (string fileName, IReadOnlyList<string> arguments) = GetInstallCommand(installer);

        InstallerProcessResult result = await _processExecutor.RunAsync(
            fileName,
            arguments,
            _options.InstallTimeout,
            cancellationToken).ConfigureAwait(false);

        if (result.ExitCode != 0)
        {
            _logger.LogError(
                "devtunnel CLI installation via '{Installer}' failed with exit code {ExitCode}. Stderr: {StandardError}",
                installer,
                result.ExitCode,
                result.StandardError);

            return new DevTunnelCliInstallResult(
                Success: false,
                InstallerUsed: installer,
                InstalledPath: null,
                StandardOutput: result.StandardOutput,
                StandardError: result.StandardError,
                FailureReason: $"Installer '{installer}' exited with code {result.ExitCode}.");
        }

        _logger.LogInformation("devtunnel CLI installed successfully via '{Installer}'.", installer);

        return new DevTunnelCliInstallResult(
            Success: true,
            InstallerUsed: installer,
            InstalledPath: null,
            StandardOutput: result.StandardOutput,
            StandardError: result.StandardError,
            FailureReason: null);
    }

    private async Task<bool> ProbeAsync(string fileName, IReadOnlyList<string> arguments, CancellationToken cancellationToken)
    {
        try
        {
            InstallerProcessResult result = await _processExecutor.RunAsync(
                fileName,
                arguments,
                TimeSpan.FromSeconds(10),
                cancellationToken).ConfigureAwait(false);

            return result.ExitCode == 0;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogDebug(ex, "Probe for '{FileName}' failed.", fileName);
            return false;
        }
    }

    private string GetNoInstallerFailureReason()
    {
        if (_platformDetector.IsWindows())
        {
            return "winget is not installed. Please install winget (App Installer) from the Microsoft Store or install the devtunnel CLI manually.";
        }

        if (_platformDetector.IsMacOS())
        {
            return "Homebrew is not installed. Please install Homebrew from https://brew.sh or install the devtunnel CLI manually.";
        }

        return "Neither curl nor wget was found. Please install curl or wget, or install the devtunnel CLI manually from https://aka.ms/DevTunnelCliInstall.";
    }

    /// <inheritdoc />
    public async ValueTask<DevTunnelCliUninstallResult> UninstallAsync(CancellationToken cancellationToken = default)
    {
        (string? uninstallerUsed, string? fileName, IReadOnlyList<string>? arguments) =
            await ResolveUninstallerAsync(cancellationToken).ConfigureAwait(false);

        if (uninstallerUsed is null || fileName is null || arguments is null)
        {
            string failureReason = GetNoUninstallerFailureReason();
            _logger.LogWarning("No supported uninstaller detected. {FailureReason}", failureReason);
            return new DevTunnelCliUninstallResult(
                Success: false,
                UninstallerUsed: string.Empty,
                StandardOutput: null,
                StandardError: null,
                FailureReason: failureReason);
        }

        _logger.LogInformation("Uninstalling devtunnel CLI using '{Uninstaller}'.", uninstallerUsed);

        InstallerProcessResult result = await _processExecutor.RunAsync(
            fileName,
            arguments,
            _options.InstallTimeout,
            cancellationToken).ConfigureAwait(false);

        // winget returns 20 when the package is not installed — treat as success because the
        // desired postcondition (CLI absent) is already satisfied. Idempotent uninstall.
        bool alreadyAbsent = uninstallerUsed == "winget" && result.ExitCode == WingetNoPackageFoundExitCode;

        if (result.ExitCode != 0 && !alreadyAbsent)
        {
            _logger.LogError(
                "devtunnel CLI uninstallation via '{Uninstaller}' failed with exit code {ExitCode}. Stderr: {StandardError}",
                uninstallerUsed,
                result.ExitCode,
                result.StandardError);

            return new DevTunnelCliUninstallResult(
                Success: false,
                UninstallerUsed: uninstallerUsed,
                StandardOutput: result.StandardOutput,
                StandardError: result.StandardError,
                FailureReason: $"Uninstaller '{uninstallerUsed}' exited with code {result.ExitCode}.");
        }

        _logger.LogInformation("devtunnel CLI uninstalled successfully via '{Uninstaller}'.", uninstallerUsed);

        return new DevTunnelCliUninstallResult(
            Success: true,
            UninstallerUsed: uninstallerUsed,
            StandardOutput: result.StandardOutput,
            StandardError: result.StandardError,
            FailureReason: null);
    }

    private async ValueTask<(string? UninstallerUsed, string? FileName, IReadOnlyList<string>? Arguments)> ResolveUninstallerAsync(
        CancellationToken cancellationToken)
    {
        if (_platformDetector.IsWindows())
        {
            bool wingetAvailable = await ProbeAsync("winget", ["--version"], cancellationToken).ConfigureAwait(false);
            if (!wingetAvailable)
            {
                return (null, null, null);
            }

            return ("winget", "winget",
                ["uninstall", "Microsoft.devtunnel", "--accept-source-agreements", "--scope", "user", "--silent"]);
        }

        if (_platformDetector.IsMacOS())
        {
            bool brewAvailable = await ProbeAsync("brew", ["--version"], cancellationToken).ConfigureAwait(false);
            if (!brewAvailable)
            {
                return (null, null, null);
            }

            return ("brew", "bash", ["-c", "HOMEBREW_NO_AUTO_UPDATE=1 brew uninstall --cask devtunnel"]);
        }

        // Linux: rm -rf is always available via bash regardless of whether curl/wget is present.
        // The Linux install script places the CLI at ~/.devtunnel; remove that directory.
        // Note: PATH entries added to shell profiles (.bashrc / .zshrc) are not removed here.
        return ("bash", "bash", ["-c", "rm -rf \"$HOME/.devtunnel\""]);
    }

    private string GetNoUninstallerFailureReason()
    {
        if (_platformDetector.IsWindows())
        {
            return "winget is not installed. Cannot uninstall devtunnel CLI automatically. Please uninstall it manually.";
        }

        if (_platformDetector.IsMacOS())
        {
            return "Homebrew is not installed. Cannot uninstall devtunnel CLI automatically. Please uninstall it manually.";
        }

        return "Could not determine uninstall method for this platform.";
    }

    private static (string FileName, IReadOnlyList<string> Arguments) GetInstallCommand(string installer)
    {
        return installer switch
        {
            // --scope user avoids UAC elevation; --silent suppresses the per-package installer UI.
            "winget" => ("winget",
                ["install", "Microsoft.devtunnel", "--accept-source-agreements", "--accept-package-agreements", "--scope", "user", "--silent"]),
            // Invoke via bash so HOMEBREW_NO_AUTO_UPDATE suppresses interactive brew self-update.
            "brew" => ("bash", ["-c", "HOMEBREW_NO_AUTO_UPDATE=1 brew install --cask devtunnel"]),
            // Linux install script is designed for non-interactive use; no extra flags needed.
            "curl" => ("bash", ["-c", "curl -sL https://aka.ms/DevTunnelCliInstall | bash"]),
            "wget" => ("bash", ["-c", "wget -qO- https://aka.ms/DevTunnelCliInstall | bash"]),
            _ => throw new ArgumentException($"Unknown installer '{installer}'.", nameof(installer)),
        };
    }
}
