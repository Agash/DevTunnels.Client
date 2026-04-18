namespace DevTunnels.Client.Installer.Internal;

internal interface IInstallerProcessExecutor
{
    Task<InstallerProcessResult> RunAsync(
        string fileName,
        IReadOnlyList<string> arguments,
        TimeSpan timeout,
        CancellationToken cancellationToken);
}

internal sealed record InstallerProcessResult(int ExitCode, string StandardOutput, string StandardError);
