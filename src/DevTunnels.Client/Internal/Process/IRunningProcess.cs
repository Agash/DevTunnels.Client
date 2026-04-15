namespace DevTunnels.Client.Internal.Process;

internal interface IRunningProcess : IAsyncDisposable
{
    event Action<bool, string>? OutputReceived;

    int? ExitCode { get; }

    Task WaitForExitAsync(CancellationToken cancellationToken);

    Task StopAsync(CancellationToken cancellationToken);
}
