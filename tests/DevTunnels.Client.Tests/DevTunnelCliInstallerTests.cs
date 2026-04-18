using DevTunnels.Client.Installer;
using DevTunnels.Client.Installer.Internal;

namespace DevTunnels.Client.Tests;

[TestClass]
public sealed class DevTunnelCliInstallerTests
{
    // -------------------------------------------------------------------------
    // DetectInstallerAsync — Windows
    // -------------------------------------------------------------------------

    [TestMethod]
    public async Task DetectInstallerAsync_OnWindows_WithWinget_ReturnsWinget()
    {
        FakeInstallerProcessExecutor executor = new FakeInstallerProcessExecutor();
        executor.EnqueueResult(new InstallerProcessResult(0, "v1.22.0", string.Empty));

        DevTunnelCliInstaller installer = CreateInstaller(executor, isWindows: true, isMacOS: false);

        string? result = await installer.DetectInstallerAsync(CancellationToken.None).ConfigureAwait(false);

        Assert.AreEqual("winget", result);
        Assert.AreEqual(1, executor.Invocations.Count);
        Assert.AreEqual("winget", executor.Invocations[0].FileName);
        CollectionAssert.AreEqual(new[] { "--version" }, (System.Collections.ICollection)executor.Invocations[0].Arguments);
    }

    [TestMethod]
    public async Task DetectInstallerAsync_OnWindows_WithoutWinget_ReturnsNull()
    {
        FakeInstallerProcessExecutor executor = new FakeInstallerProcessExecutor();
        executor.EnqueueResult(new InstallerProcessResult(1, string.Empty, "not found"));

        DevTunnelCliInstaller installer = CreateInstaller(executor, isWindows: true, isMacOS: false);

        string? result = await installer.DetectInstallerAsync(CancellationToken.None).ConfigureAwait(false);

        Assert.IsNull(result);
    }

    // -------------------------------------------------------------------------
    // DetectInstallerAsync — macOS
    // -------------------------------------------------------------------------

    [TestMethod]
    public async Task DetectInstallerAsync_OnMacOS_WithBrew_ReturnsBrew()
    {
        FakeInstallerProcessExecutor executor = new FakeInstallerProcessExecutor();
        executor.EnqueueResult(new InstallerProcessResult(0, "Homebrew 4.0.0", string.Empty));

        DevTunnelCliInstaller installer = CreateInstaller(executor, isWindows: false, isMacOS: true);

        string? result = await installer.DetectInstallerAsync(CancellationToken.None).ConfigureAwait(false);

        Assert.AreEqual("brew", result);
        Assert.AreEqual(1, executor.Invocations.Count);
        Assert.AreEqual("brew", executor.Invocations[0].FileName);
        CollectionAssert.AreEqual(new[] { "--version" }, (System.Collections.ICollection)executor.Invocations[0].Arguments);
    }

    [TestMethod]
    public async Task DetectInstallerAsync_OnMacOS_WithoutBrew_ReturnsNull()
    {
        FakeInstallerProcessExecutor executor = new FakeInstallerProcessExecutor();
        executor.EnqueueResult(new InstallerProcessResult(127, string.Empty, "brew: command not found"));

        DevTunnelCliInstaller installer = CreateInstaller(executor, isWindows: false, isMacOS: true);

        string? result = await installer.DetectInstallerAsync(CancellationToken.None).ConfigureAwait(false);

        Assert.IsNull(result);
    }

    // -------------------------------------------------------------------------
    // DetectInstallerAsync — Linux
    // -------------------------------------------------------------------------

    [TestMethod]
    public async Task DetectInstallerAsync_OnLinux_WithCurl_ReturnsCurl()
    {
        FakeInstallerProcessExecutor executor = new FakeInstallerProcessExecutor();
        // curl --version succeeds
        executor.EnqueueResult(new InstallerProcessResult(0, "curl 8.0.0", string.Empty));

        DevTunnelCliInstaller installer = CreateInstaller(executor, isWindows: false, isMacOS: false);

        string? result = await installer.DetectInstallerAsync(CancellationToken.None).ConfigureAwait(false);

        Assert.AreEqual("curl", result);
        Assert.AreEqual(1, executor.Invocations.Count);
        Assert.AreEqual("curl", executor.Invocations[0].FileName);
    }

    [TestMethod]
    public async Task DetectInstallerAsync_OnLinux_WithoutCurl_WithWget_ReturnsWget()
    {
        FakeInstallerProcessExecutor executor = new FakeInstallerProcessExecutor();
        // curl probe fails
        executor.EnqueueResult(new InstallerProcessResult(127, string.Empty, "curl: not found"));
        // wget probe succeeds
        executor.EnqueueResult(new InstallerProcessResult(0, "GNU Wget 1.21.0", string.Empty));

        DevTunnelCliInstaller installer = CreateInstaller(executor, isWindows: false, isMacOS: false);

        string? result = await installer.DetectInstallerAsync(CancellationToken.None).ConfigureAwait(false);

        Assert.AreEqual("wget", result);
        Assert.AreEqual(2, executor.Invocations.Count);
        Assert.AreEqual("curl", executor.Invocations[0].FileName);
        Assert.AreEqual("wget", executor.Invocations[1].FileName);
    }

    [TestMethod]
    public async Task DetectInstallerAsync_OnLinux_WithNeitherCurlNorWget_ReturnsNull()
    {
        FakeInstallerProcessExecutor executor = new FakeInstallerProcessExecutor();
        executor.EnqueueResult(new InstallerProcessResult(127, string.Empty, "curl: not found"));
        executor.EnqueueResult(new InstallerProcessResult(127, string.Empty, "wget: not found"));

        DevTunnelCliInstaller installer = CreateInstaller(executor, isWindows: false, isMacOS: false);

        string? result = await installer.DetectInstallerAsync(CancellationToken.None).ConfigureAwait(false);

        Assert.IsNull(result);
        Assert.AreEqual(2, executor.Invocations.Count);
    }

    // -------------------------------------------------------------------------
    // InstallAsync — Windows
    // -------------------------------------------------------------------------

    [TestMethod]
    public async Task InstallAsync_OnWindows_WithWinget_RunsCorrectCommand()
    {
        FakeInstallerProcessExecutor executor = new FakeInstallerProcessExecutor();
        // DetectInstallerAsync probe
        executor.EnqueueResult(new InstallerProcessResult(0, "v1.22.0", string.Empty));
        // Install command
        executor.EnqueueResult(new InstallerProcessResult(0, "Successfully installed.", string.Empty));

        DevTunnelCliInstaller installer = CreateInstaller(executor, isWindows: true, isMacOS: false);

        DevTunnelCliInstallResult result = await installer.InstallAsync(CancellationToken.None).ConfigureAwait(false);

        Assert.IsTrue(result.Success);
        Assert.AreEqual("winget", result.InstallerUsed);
        Assert.IsNull(result.FailureReason);

        // Second invocation is the install command
        FakeInstallerInvocation installInvocation = executor.Invocations[1];
        Assert.AreEqual("winget", installInvocation.FileName);
        CollectionAssert.AreEqual(
            new[] { "install", "Microsoft.devtunnel", "--accept-source-agreements", "--accept-package-agreements", "--scope", "user", "--silent" },
            (System.Collections.ICollection)installInvocation.Arguments);
    }

    [TestMethod]
    public async Task InstallAsync_OnWindows_WithoutWinget_ReturnsFailure()
    {
        FakeInstallerProcessExecutor executor = new FakeInstallerProcessExecutor();
        executor.EnqueueResult(new InstallerProcessResult(1, string.Empty, "winget not found"));

        DevTunnelCliInstaller installer = CreateInstaller(executor, isWindows: true, isMacOS: false);

        DevTunnelCliInstallResult result = await installer.InstallAsync(CancellationToken.None).ConfigureAwait(false);

        Assert.IsFalse(result.Success);
        Assert.AreEqual(string.Empty, result.InstallerUsed);
        Assert.IsNotNull(result.FailureReason);
        StringAssert.Contains(result.FailureReason, "winget");
    }

    // -------------------------------------------------------------------------
    // InstallAsync — macOS
    // -------------------------------------------------------------------------

    [TestMethod]
    public async Task InstallAsync_OnMacOS_WithBrew_RunsCorrectCommand()
    {
        FakeInstallerProcessExecutor executor = new FakeInstallerProcessExecutor();
        // DetectInstallerAsync probe
        executor.EnqueueResult(new InstallerProcessResult(0, "Homebrew 4.0.0", string.Empty));
        // Install command
        executor.EnqueueResult(new InstallerProcessResult(0, "==> Installing devtunnel", string.Empty));

        DevTunnelCliInstaller installer = CreateInstaller(executor, isWindows: false, isMacOS: true);

        DevTunnelCliInstallResult result = await installer.InstallAsync(CancellationToken.None).ConfigureAwait(false);

        Assert.IsTrue(result.Success);
        Assert.AreEqual("brew", result.InstallerUsed);

        FakeInstallerInvocation installInvocation = executor.Invocations[1];
        Assert.AreEqual("bash", installInvocation.FileName);
        CollectionAssert.AreEqual(
            new[] { "-c", "HOMEBREW_NO_AUTO_UPDATE=1 brew install --cask devtunnel" },
            (System.Collections.ICollection)installInvocation.Arguments);
    }

    // -------------------------------------------------------------------------
    // InstallAsync — Linux
    // -------------------------------------------------------------------------

    [TestMethod]
    public async Task InstallAsync_OnLinux_WithCurl_RunsCorrectCommand()
    {
        FakeInstallerProcessExecutor executor = new FakeInstallerProcessExecutor();
        // DetectInstallerAsync probe — curl present
        executor.EnqueueResult(new InstallerProcessResult(0, "curl 8.0.0", string.Empty));
        // Install command
        executor.EnqueueResult(new InstallerProcessResult(0, "devtunnel installed.", string.Empty));

        DevTunnelCliInstaller installer = CreateInstaller(executor, isWindows: false, isMacOS: false);

        DevTunnelCliInstallResult result = await installer.InstallAsync(CancellationToken.None).ConfigureAwait(false);

        Assert.IsTrue(result.Success);
        Assert.AreEqual("curl", result.InstallerUsed);

        FakeInstallerInvocation installInvocation = executor.Invocations[1];
        Assert.AreEqual("bash", installInvocation.FileName);
        CollectionAssert.AreEqual(
            new[] { "-c", "curl -sL https://aka.ms/DevTunnelCliInstall | bash" },
            (System.Collections.ICollection)installInvocation.Arguments);
    }

    // -------------------------------------------------------------------------
    // InstallAsync — failure from install command
    // -------------------------------------------------------------------------

    [TestMethod]
    public async Task InstallAsync_WhenInstallCommandFails_ReturnsFailure()
    {
        FakeInstallerProcessExecutor executor = new FakeInstallerProcessExecutor();
        // DetectInstallerAsync probe — winget available
        executor.EnqueueResult(new InstallerProcessResult(0, "v1.22.0", string.Empty));
        // Install command exits with error
        executor.EnqueueResult(new InstallerProcessResult(1, string.Empty, "Installation failed."));

        DevTunnelCliInstaller installer = CreateInstaller(executor, isWindows: true, isMacOS: false);

        DevTunnelCliInstallResult result = await installer.InstallAsync(CancellationToken.None).ConfigureAwait(false);

        Assert.IsFalse(result.Success);
        Assert.AreEqual("winget", result.InstallerUsed);
        Assert.IsNotNull(result.FailureReason);
        StringAssert.Contains(result.FailureReason, "1");
        Assert.AreEqual("Installation failed.", result.StandardError);
    }

    // -------------------------------------------------------------------------
    // UninstallAsync — Windows
    // -------------------------------------------------------------------------

    [TestMethod]
    public async Task UninstallAsync_OnWindows_WithWinget_RunsCorrectCommand()
    {
        FakeInstallerProcessExecutor executor = new FakeInstallerProcessExecutor();
        // winget probe for uninstall detection
        executor.EnqueueResult(new InstallerProcessResult(0, "v1.22.0", string.Empty));
        // Uninstall command
        executor.EnqueueResult(new InstallerProcessResult(0, "Successfully uninstalled.", string.Empty));

        DevTunnelCliInstaller installer = CreateInstaller(executor, isWindows: true, isMacOS: false);

        DevTunnelCliUninstallResult result = await installer.UninstallAsync(CancellationToken.None).ConfigureAwait(false);

        Assert.IsTrue(result.Success);
        Assert.AreEqual("winget", result.UninstallerUsed);
        Assert.IsNull(result.FailureReason);

        FakeInstallerInvocation uninstallInvocation = executor.Invocations[1];
        Assert.AreEqual("winget", uninstallInvocation.FileName);
        CollectionAssert.AreEqual(
            new[] { "uninstall", "Microsoft.devtunnel", "--accept-source-agreements", "--scope", "user", "--silent" },
            (System.Collections.ICollection)uninstallInvocation.Arguments);
    }

    [TestMethod]
    public async Task UninstallAsync_OnWindows_WithoutWinget_ReturnsFailure()
    {
        FakeInstallerProcessExecutor executor = new FakeInstallerProcessExecutor();
        // winget probe fails
        executor.EnqueueResult(new InstallerProcessResult(1, string.Empty, "winget not found"));

        DevTunnelCliInstaller installer = CreateInstaller(executor, isWindows: true, isMacOS: false);

        DevTunnelCliUninstallResult result = await installer.UninstallAsync(CancellationToken.None).ConfigureAwait(false);

        Assert.IsFalse(result.Success);
        Assert.AreEqual(string.Empty, result.UninstallerUsed);
        Assert.IsNotNull(result.FailureReason);
        StringAssert.Contains(result.FailureReason, "winget");
    }

    // -------------------------------------------------------------------------
    // UninstallAsync — macOS
    // -------------------------------------------------------------------------

    [TestMethod]
    public async Task UninstallAsync_OnMacOS_WithBrew_RunsCorrectCommand()
    {
        FakeInstallerProcessExecutor executor = new FakeInstallerProcessExecutor();
        // brew probe for uninstall detection
        executor.EnqueueResult(new InstallerProcessResult(0, "Homebrew 4.0.0", string.Empty));
        // Uninstall command
        executor.EnqueueResult(new InstallerProcessResult(0, "Uninstalling devtunnel", string.Empty));

        DevTunnelCliInstaller installer = CreateInstaller(executor, isWindows: false, isMacOS: true);

        DevTunnelCliUninstallResult result = await installer.UninstallAsync(CancellationToken.None).ConfigureAwait(false);

        Assert.IsTrue(result.Success);
        Assert.AreEqual("brew", result.UninstallerUsed);

        FakeInstallerInvocation uninstallInvocation = executor.Invocations[1];
        Assert.AreEqual("bash", uninstallInvocation.FileName);
        CollectionAssert.AreEqual(
            new[] { "-c", "HOMEBREW_NO_AUTO_UPDATE=1 brew uninstall --cask devtunnel" },
            (System.Collections.ICollection)uninstallInvocation.Arguments);
    }

    [TestMethod]
    public async Task UninstallAsync_OnMacOS_WithoutBrew_ReturnsFailure()
    {
        FakeInstallerProcessExecutor executor = new FakeInstallerProcessExecutor();
        // brew probe fails
        executor.EnqueueResult(new InstallerProcessResult(127, string.Empty, "brew: command not found"));

        DevTunnelCliInstaller installer = CreateInstaller(executor, isWindows: false, isMacOS: true);

        DevTunnelCliUninstallResult result = await installer.UninstallAsync(CancellationToken.None).ConfigureAwait(false);

        Assert.IsFalse(result.Success);
        Assert.AreEqual(string.Empty, result.UninstallerUsed);
        Assert.IsNotNull(result.FailureReason);
        StringAssert.Contains(result.FailureReason, "Homebrew");
    }

    // -------------------------------------------------------------------------
    // UninstallAsync — Linux
    // -------------------------------------------------------------------------

    [TestMethod]
    public async Task UninstallAsync_OnLinux_RunsBashRemoveCommand()
    {
        // No process results needed for detection — Linux uninstall skips the probe.
        FakeInstallerProcessExecutor executor = new FakeInstallerProcessExecutor();
        executor.EnqueueResult(new InstallerProcessResult(0, string.Empty, string.Empty));

        DevTunnelCliInstaller installer = CreateInstaller(executor, isWindows: false, isMacOS: false);

        DevTunnelCliUninstallResult result = await installer.UninstallAsync(CancellationToken.None).ConfigureAwait(false);

        Assert.IsTrue(result.Success);
        Assert.AreEqual("bash", result.UninstallerUsed);

        Assert.AreEqual(1, executor.Invocations.Count);
        FakeInstallerInvocation uninstallInvocation = executor.Invocations[0];
        Assert.AreEqual("bash", uninstallInvocation.FileName);
        CollectionAssert.AreEqual(
            new[] { "-c", "rm -rf \"$HOME/.devtunnel\"" },
            (System.Collections.ICollection)uninstallInvocation.Arguments);
    }

    [TestMethod]
    public async Task UninstallAsync_OnWindows_WhenPackageNotFound_ExitCode20_ReturnsSuccess()
    {
        // winget exits with code 20 (APPINSTALLER_CLI_ERROR_NO_APPLICATIONS_FOUND) when the
        // package is already absent. Uninstall should treat this as success — the desired
        // postcondition (CLI not present) is already satisfied.
        FakeInstallerProcessExecutor executor = new FakeInstallerProcessExecutor();
        // winget probe succeeds
        executor.EnqueueResult(new InstallerProcessResult(0, "v1.22.0", string.Empty));
        // Uninstall command exits with 20 — package not installed
        executor.EnqueueResult(new InstallerProcessResult(20, string.Empty, string.Empty));

        DevTunnelCliInstaller installer = CreateInstaller(executor, isWindows: true, isMacOS: false);

        DevTunnelCliUninstallResult result = await installer.UninstallAsync(CancellationToken.None).ConfigureAwait(false);

        Assert.IsTrue(result.Success);
        Assert.AreEqual("winget", result.UninstallerUsed);
        Assert.IsNull(result.FailureReason);
    }

    [TestMethod]
    public async Task UninstallAsync_WhenUninstallCommandFails_ReturnsFailure()
    {
        FakeInstallerProcessExecutor executor = new FakeInstallerProcessExecutor();
        // winget probe succeeds
        executor.EnqueueResult(new InstallerProcessResult(0, "v1.22.0", string.Empty));
        // Uninstall command exits with error
        executor.EnqueueResult(new InstallerProcessResult(1, string.Empty, "No installed package found."));

        DevTunnelCliInstaller installer = CreateInstaller(executor, isWindows: true, isMacOS: false);

        DevTunnelCliUninstallResult result = await installer.UninstallAsync(CancellationToken.None).ConfigureAwait(false);

        Assert.IsFalse(result.Success);
        Assert.AreEqual("winget", result.UninstallerUsed);
        Assert.IsNotNull(result.FailureReason);
        StringAssert.Contains(result.FailureReason, "1");
        Assert.AreEqual("No installed package found.", result.StandardError);
    }

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    private static DevTunnelCliInstaller CreateInstaller(
        FakeInstallerProcessExecutor executor,
        bool isWindows,
        bool isMacOS)
    {
        DevTunnelCliInstallerOptions options = new DevTunnelCliInstallerOptions();
        FakePlatformDetector platformDetector = new FakePlatformDetector(isWindows, isMacOS);
        return new DevTunnelCliInstaller(options, logger: null, executor, platformDetector);
    }
}

// ---------------------------------------------------------------------------
// Test doubles
// ---------------------------------------------------------------------------

internal sealed record FakeInstallerInvocation(string FileName, IReadOnlyList<string> Arguments);

internal sealed class FakeInstallerProcessExecutor : IInstallerProcessExecutor
{
    private readonly Queue<InstallerProcessResult> _results = new();

    public List<FakeInstallerInvocation> Invocations { get; } = [];

    public void EnqueueResult(InstallerProcessResult result) => _results.Enqueue(result);

    public Task<InstallerProcessResult> RunAsync(
        string fileName,
        IReadOnlyList<string> arguments,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        Invocations.Add(new FakeInstallerInvocation(fileName, arguments));
        return _results.Count == 0
            ? throw new InvalidOperationException("No queued installer process result was available.")
            : Task.FromResult(_results.Dequeue());
    }
}

internal sealed class FakePlatformDetector : IPlatformDetector
{
    private readonly bool _isWindows;
    private readonly bool _isMacOS;

    public FakePlatformDetector(bool isWindows, bool isMacOS)
    {
        _isWindows = isWindows;
        _isMacOS = isMacOS;
    }

    public bool IsWindows() => _isWindows;
    public bool IsMacOS() => _isMacOS;
}
