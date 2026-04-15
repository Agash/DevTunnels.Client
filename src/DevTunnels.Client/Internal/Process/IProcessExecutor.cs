using System.Text;

namespace DevTunnels.Client.Internal.Process;

internal interface IProcessExecutor
{
    Task<ProcessExecutionResult> RunAsync(ProcessSpec processSpec, CancellationToken cancellationToken);

    Task<IRunningProcess> StartAsync(ProcessSpec processSpec, CancellationToken cancellationToken);
}

internal sealed record ProcessSpec(
    string FileName,
    IReadOnlyList<string> Arguments,
    bool UseShellExecute,
    string? WorkingDirectory,
    Encoding? StandardOutputEncoding = null,
    Encoding? StandardErrorEncoding = null);

internal sealed record ProcessExecutionResult(int ExitCode, string StandardOutput, string StandardError);
