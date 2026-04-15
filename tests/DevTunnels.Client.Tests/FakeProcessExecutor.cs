using DevTunnels.Client.Internal;
using DevTunnels.Client.Internal.Process;

namespace DevTunnels.Client.Tests;

internal sealed class FakeProcessExecutor : IProcessExecutor
{
    private readonly Queue<Func<ProcessSpec, ProcessExecutionResult>> _runResults = new();
    private readonly Queue<FakeRunningProcess> _runningProcesses = new();

    public List<ProcessSpec> RunInvocations { get; } = [];

    public List<ProcessSpec> StartInvocations { get; } = [];

    public void EnqueueRunResult(Func<ProcessSpec, ProcessExecutionResult> resultFactory) => _runResults.Enqueue(resultFactory);

    public void EnqueueRunResult(ProcessExecutionResult result) => _runResults.Enqueue(_ => result);

    public void EnqueueRunningProcess(FakeRunningProcess process) => _runningProcesses.Enqueue(process);

    public Task<ProcessExecutionResult> RunAsync(ProcessSpec processSpec, CancellationToken cancellationToken)
    {
        RunInvocations.Add(processSpec);
        return _runResults.Count == 0
            ? throw new InvalidOperationException("No queued process result was available.")
            : Task.FromResult(_runResults.Dequeue()(processSpec));
    }

    public Task<IRunningProcess> StartAsync(ProcessSpec processSpec, CancellationToken cancellationToken)
    {
        StartInvocations.Add(processSpec);
        return _runningProcesses.Count == 0
            ? throw new InvalidOperationException("No queued running process was available.")
            : Task.FromResult<IRunningProcess>(_runningProcesses.Dequeue());
    }
}

internal sealed class FakeRunningProcess : IRunningProcess
{
    private readonly TaskCompletionSource _exitTcs = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public event Action<bool, string>? OutputReceived;

    public int? ExitCode { get; private set; }

    public void EmitStdOut(string line) => OutputReceived?.Invoke(false, line);

    public void EmitStdErr(string line) => OutputReceived?.Invoke(true, line);

    public void Complete(int exitCode = 0)
    {
        ExitCode = exitCode;
        _ = _exitTcs.TrySetResult();
    }

    public async Task WaitForExitAsync(CancellationToken cancellationToken)
    {
        using CancellationTokenRegistration ctr = cancellationToken.Register(() => _exitTcs.TrySetCanceled(cancellationToken));
        await _exitTcs.Task.ConfigureAwait(false);
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        Complete();
        return Task.CompletedTask;
    }

    public ValueTask DisposeAsync()
    {
        Complete();
        return ValueTask.CompletedTask;
    }
}
