using System.Diagnostics;
using System.Text;

namespace DevTunnels.Client.Installer.Internal;

internal sealed class SystemInstallerProcessExecutor : IInstallerProcessExecutor
{
    public async Task<InstallerProcessResult> RunAsync(
        string fileName,
        IReadOnlyList<string> arguments,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        ProcessStartInfo startInfo = new ProcessStartInfo
        {
            FileName = fileName,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            RedirectStandardInput = false,
            CreateNoWindow = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
        };

        foreach (string argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        using Process process = new Process
        {
            StartInfo = startInfo,
            EnableRaisingEvents = true,
        };

        if (!process.Start())
        {
            throw new InvalidOperationException($"Failed to start process '{fileName}'.");
        }

        using CancellationTokenSource timeoutCts = new CancellationTokenSource(timeout);
        using CancellationTokenSource linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

        CancellationToken combinedToken = linkedCts.Token;

        Task<string> stdoutTask = process.StandardOutput.ReadToEndAsync(combinedToken);
        Task<string> stderrTask = process.StandardError.ReadToEndAsync(combinedToken);

        try
        {
            await process.WaitForExitAsync(combinedToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // Kill on both timeout AND user cancellation to avoid orphaned installer processes.
            // Unlike the tunnel host (which has no children), installers like winget and brew
            // spawn child sub-installer processes that must also be terminated — hence
            // entireProcessTree:true. This catch runs after ConfigureAwait(false) so it is
            // already on a thread pool thread, not the calling/UI thread.
            try
            {
                if (!process.HasExited)
                    process.Kill(entireProcessTree: true);
            }
            catch { }

            if (timeoutCts.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
                throw new TimeoutException($"Process '{fileName}' did not exit within the allowed timeout of {timeout}.");

            throw;
        }

        string stdout = await stdoutTask.ConfigureAwait(false);
        string stderr = await stderrTask.ConfigureAwait(false);

        return new InstallerProcessResult(process.ExitCode, stdout, stderr);
    }
}
