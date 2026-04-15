using System.Text;
using SysProcess = System.Diagnostics.Process;
using SysProcessStartInfo = System.Diagnostics.ProcessStartInfo;

namespace DevTunnels.Client.Internal.Process;

internal sealed class SystemProcessExecutor : IProcessExecutor
{
    public async Task<ProcessExecutionResult> RunAsync(ProcessSpec processSpec, CancellationToken cancellationToken)
    {
        await using SystemRunningProcess running = await StartInternalAsync(processSpec, cancellationToken).ConfigureAwait(false);
        await running.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
        return new ProcessExecutionResult(running.ExitCode ?? -1, running.StandardOutput.ToString(), running.StandardError.ToString());
    }

    public Task<IRunningProcess> StartAsync(ProcessSpec processSpec, CancellationToken cancellationToken) =>
        StartInternalAsync(processSpec, cancellationToken).ContinueWith(static t => (IRunningProcess)t.Result, cancellationToken);

    private static Task<SystemRunningProcess> StartInternalAsync(ProcessSpec processSpec, CancellationToken cancellationToken)
    {
        var startInfo = new SysProcessStartInfo
        {
            FileName = processSpec.FileName,
            UseShellExecute = processSpec.UseShellExecute,
            RedirectStandardOutput = !processSpec.UseShellExecute,
            RedirectStandardError = !processSpec.UseShellExecute,
            RedirectStandardInput = false,
            CreateNoWindow = true,
            WorkingDirectory = processSpec.WorkingDirectory ?? Environment.CurrentDirectory
        };

        foreach (string argument in processSpec.Arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        if (!processSpec.UseShellExecute)
        {
            startInfo.StandardOutputEncoding = processSpec.StandardOutputEncoding ?? Encoding.UTF8;
            startInfo.StandardErrorEncoding = processSpec.StandardErrorEncoding ?? Encoding.UTF8;
        }

        var process = new SysProcess
        {
            StartInfo = startInfo,
            EnableRaisingEvents = true
        };

        if (!process.Start())
        {
            throw new InvalidOperationException($"Failed to start process '{processSpec.FileName}'.");
        }

        var running = new SystemRunningProcess(process, processSpec.UseShellExecute);
        running.AttachCancellation(cancellationToken);
        return Task.FromResult(running);
    }

    private sealed class SystemRunningProcess : IRunningProcess
    {
        private readonly SysProcess _process;
        private readonly Task _stdoutPump;
        private readonly Task _stderrPump;
        private readonly TaskCompletionSource _exitTcs = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private CancellationTokenRegistration _cancellationRegistration;

        public SystemRunningProcess(SysProcess process, bool useShellExecute)
        {
            _process = process;
            StandardOutput = new StringBuilder();
            StandardError = new StringBuilder();

            _process.Exited += (_, _) => _exitTcs.TrySetResult();

            if (useShellExecute)
            {
                _stdoutPump = Task.CompletedTask;
                _stderrPump = Task.CompletedTask;
            }
            else
            {
                _stdoutPump = PumpAsync(_process.StandardOutput, isError: false);
                _stderrPump = PumpAsync(_process.StandardError, isError: true);
            }
        }

        public event Action<bool, string>? OutputReceived;

        public int? ExitCode => _process.HasExited ? _process.ExitCode : null;

        public StringBuilder StandardOutput { get; }

        public StringBuilder StandardError { get; }

        public void AttachCancellation(CancellationToken cancellationToken) => _cancellationRegistration = cancellationToken.Register(() =>
                                                                                        {
                                                                                            try
                                                                                            {
                                                                                                if (!_process.HasExited)
                                                                                                {
                                                                                                    _process.Kill(entireProcessTree: true);
                                                                                                }
                                                                                            }
                                                                                            catch
                                                                                            {
                                                                                                // ignored
                                                                                            }
                                                                                        });

        public async Task WaitForExitAsync(CancellationToken cancellationToken)
        {
            using CancellationTokenRegistration ctr = cancellationToken.Register(() => _exitTcs.TrySetCanceled(cancellationToken));
            await _exitTcs.Task.ConfigureAwait(false);
            await Task.WhenAll(_stdoutPump, _stderrPump).ConfigureAwait(false);
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            try
            {
                if (!_process.HasExited)
                {
                    _process.Kill(entireProcessTree: true);
                }
            }
            catch
            {
                // ignored
            }

            return WaitForExitAsync(cancellationToken);
        }

        public async ValueTask DisposeAsync()
        {
            _cancellationRegistration.Dispose();
            _process.Dispose();
            await Task.CompletedTask.ConfigureAwait(false);
        }

        private async Task PumpAsync(StreamReader reader, bool isError)
        {
            while (true)
            {
                string? line = await reader.ReadLineAsync().ConfigureAwait(false);
                if (line is null)
                {
                    break;
                }

                _ = isError ? StandardError.AppendLine(line) : StandardOutput.AppendLine(line);

                OutputReceived?.Invoke(isError, line);
            }
        }
    }
}
