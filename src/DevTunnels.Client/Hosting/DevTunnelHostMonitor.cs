using System.Threading.Channels;
using DevTunnels.Client.Internal;
using DevTunnels.Client.Ports;
using DevTunnels.Client.Tunnels;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace DevTunnels.Client.Hosting;

/// <summary>
/// Follows one host session for as long as it runs: readiness, relay disconnects and reconnects, another host
/// taking the tunnel, and the host's exit. It reads the host's output first because that is immediate and
/// local, and asks the service on an interval, and immediately when the output is not understood.
/// Every observation goes through one queue, so a slow service answer cannot overwrite a newer local one.
/// </summary>
public sealed class DevTunnelHostMonitor : IAsyncDisposable
{
    private readonly IDevTunnelsClient _client;
    private readonly IDevTunnelHostSession _session;
    private readonly DevTunnelHostMonitorOptions _options;
    private readonly TimeProvider _time;
    private readonly ILogger _logger;
    private readonly DevTunnelHostOutputParser _parser;
    private readonly CancellationTokenSource _stopping = new();
    private readonly Channel<Func<ValueTask>> _queue = Channel.CreateUnbounded<Func<ValueTask>>(
        new UnboundedChannelOptions { SingleReader = true, AllowSynchronousContinuations = false }
    );
    private readonly TaskCompletionSource _connected = new(
        TaskCreationOptions.RunContinuationsAsynchronously
    );
    private readonly Dictionary<int, Uri> _ports = [];
    private readonly Task _processing;
    private readonly Task _reconciling;
    private readonly Task _watchingExit;

    private DevTunnelHostStatus _status;
    private Task _query = Task.CompletedTask;
    private long _revision;
    private bool _hasLocalEvidence;
    private bool _locallyDisconnected;
    private bool _serviceConfirmed;
    private bool _warnedAboutOutput;
    private int _disposed;

    /// <summary>Initializes a new instance of the <see cref="DevTunnelHostMonitor"/> class and starts it.</summary>
    /// <param name="client">The client, for asking the service.</param>
    /// <param name="session">The session to follow. Disposing the monitor leaves it running.</param>
    /// <param name="options">The options, or null for the defaults.</param>
    /// <param name="timeProvider">The clock, or null for the system's.</param>
    /// <param name="logger">The logger, or null.</param>
    public DevTunnelHostMonitor(
        IDevTunnelsClient client,
        IDevTunnelHostSession session,
        DevTunnelHostMonitorOptions? options = null,
        TimeProvider? timeProvider = null,
        ILogger<DevTunnelHostMonitor>? logger = null
    )
    {
        ArgumentNullException.ThrowIfNull(client);
        ArgumentNullException.ThrowIfNull(session);
        ArgumentException.ThrowIfNullOrWhiteSpace(session.TunnelId);

        _client = client;
        _session = session;
        _options = options ?? new DevTunnelHostMonitorOptions();
        _time = timeProvider ?? TimeProvider.System;
        _logger = logger ?? NullLogger<DevTunnelHostMonitor>.Instance;
        _parser = new DevTunnelHostOutputParser(session.TunnelId);
        _status = new DevTunnelHostStatus
        {
            TunnelId = session.TunnelId,
            ObservedAt = _time.GetUtcNow(),
        };

        session.OutputReceived += HandleOutputReceived;
        _processing = ProcessAsync();

        // Output printed before this monitor existed is gone; a running session already proved readiness.
        if (session.State is DevTunnelHostState.Running)
        {
            Post(MarkConnected);
        }

        _reconciling = ReconcileLoopAsync();
        _watchingExit = WatchExitAsync();
    }

    /// <summary>Raised on the monitor's queue when the status changes. Handlers must not block.</summary>
    public event EventHandler<DevTunnelHostStatusChangedEventArgs>? StatusChanged;

    /// <summary>Gets the latest status.</summary>
    public DevTunnelHostStatus Status => Volatile.Read(ref _status);

    /// <summary>Waits until this host has connected. A host that stops first fails the wait.</summary>
    /// <param name="cancellationToken">Cancels the wait.</param>
    /// <returns>A task that completes once connected.</returns>
    public Task WaitForConnectedAsync(CancellationToken cancellationToken = default) =>
        _connected.Task.WaitAsync(cancellationToken);

    /// <summary>Asks the service now, joining a question already in flight.</summary>
    /// <param name="cancellationToken">Cancels waiting; the question itself continues.</param>
    /// <returns>A task that completes when the answer has been applied.</returns>
    public async Task ReconcileAsync(CancellationToken cancellationToken = default)
    {
        TaskCompletionSource<Task> asked = new(TaskCreationOptions.RunContinuationsAsynchronously);
        Post(() =>
        {
            asked.TrySetResult(BeginQuery());
            return ValueTask.CompletedTask;
        });
        Task query = await asked.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
        await query.WaitAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }

        _session.OutputReceived -= HandleOutputReceived;
        await _stopping.CancelAsync().ConfigureAwait(false);
        _ = _queue.Writer.TryComplete();
        await Task.WhenAll(_processing, _reconciling, _watchingExit).ConfigureAwait(false);
        await _query.ConfigureAwait(false);
        _ = _connected.TrySetCanceled();
        _stopping.Dispose();
    }

    private void HandleOutputReceived(object? sender, DevTunnelHostOutputReceivedEventArgs e) =>
        Post(() =>
        {
            foreach (string message in DevTunnelHostOutputParser.SplitOutput(e.Line))
            {
                Apply(_parser.Parse(message));
            }

            return ValueTask.CompletedTask;
        });

    private void Post(Func<ValueTask> operation) => _ = _queue.Writer.TryWrite(operation);

    private void Post(Action operation) =>
        Post(() =>
        {
            operation();
            return ValueTask.CompletedTask;
        });

    private async Task ProcessAsync()
    {
        await foreach (
            Func<ValueTask> operation in _queue.Reader.ReadAllAsync().ConfigureAwait(false)
        )
        {
            try
            {
                await operation().ConfigureAwait(false);
            }
            catch (Exception exception) when (exception is not OutOfMemoryException)
            {
                _logger.LogError(exception, "Applying a dev tunnel host observation failed.");
            }
        }
    }

    private void Apply(DevTunnelHostOutput output)
    {
        switch (output.Kind)
        {
            case DevTunnelHostOutputKind.Port:
                _ports[output.Port!.Value] = output.PortUri!;
                Publish(_status with { Ports = new Dictionary<int, Uri>(_ports) });
                break;
            case DevTunnelHostOutputKind.Ready:
            case DevTunnelHostOutputKind.Connected:
                MarkConnected();
                break;
            case DevTunnelHostOutputKind.Disconnected:
                MarkDisconnected();
                break;
            case DevTunnelHostOutputKind.Unrecognized:
                WarnUnrecognizedOutput();
                break;
            case DevTunnelHostOutputKind.None:
            default:
                break;
        }
    }

    private void MarkConnected()
    {
        _revision++;
        _hasLocalEvidence = true;
        _locallyDisconnected = false;
        Publish(
            _status with
            {
                Connectivity = DevTunnelHostConnectivity.Connected,
                IsTakenOver = false,
            }
        );
        _ = _connected.TrySetResult();
    }

    private void MarkDisconnected()
    {
        _revision++;
        _locallyDisconnected = true;
        Publish(
            _status with
            {
                Connectivity = DevTunnelHostConnectivity.Disconnected,
                IsTakenOver = _status.HostConnections > 0,
            }
        );
        _ = BeginQuery();
    }

    private void WarnUnrecognizedOutput()
    {
        // Only once, and bounded: the point is to fall back, not to repeat the console.
        if (!_warnedAboutOutput)
        {
            _warnedAboutOutput = true;
            _logger.LogDebug(
                "Unrecognized dev tunnel host output for {TunnelId}; relying on the service.",
                _status.TunnelId
            );
        }

        _ = BeginQuery();
    }

    private Task BeginQuery()
    {
        // Overlapping questions coalesce, and no call to the CLI ever holds up the queue.
        if (_query.IsCompleted && !_stopping.IsCancellationRequested)
        {
            _query = QueryServiceAsync(_revision);
        }

        return _query;
    }

    private async Task QueryServiceAsync(long revision)
    {
        CancellationToken stopping = _stopping.Token;
        try
        {
            DevTunnelStatus status = await _client
                .GetTunnelAsync(_status.TunnelId!, stopping)
                .ConfigureAwait(false);
            Post(() => ApplyService(status, revision));
        }
        catch (OperationCanceledException) when (stopping.IsCancellationRequested)
        {
            // Deliberately not logged: the monitor is stopping.
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            _logger.LogWarning(exception, "Asking the service about a dev tunnel failed.");
            Post(() => Publish(_status with { Error = exception.Message }));
        }
    }

    private void ApplyService(DevTunnelStatus status, long revision)
    {
        Dictionary<int, Uri> ports = new(_ports);
        foreach (DevTunnelPort port in status.Ports)
        {
            // The service identifies the port itself, so no assumption about its hostname applies here.
            if (port.PortUri is { IsAbsoluteUri: true, Scheme: "https" } uri)
            {
                ports[port.PortNumber] = uri;
                _ports[port.PortNumber] = uri;
            }
        }

        bool current = revision == _revision;
        if (current && status.HostConnections == 0 && _serviceConfirmed)
        {
            // Zero before the service ever saw this host can be startup propagation. Afterwards it is a loss,
            // and a later positive count may be another machine, so new local evidence is required.
            _hasLocalEvidence = false;
            _serviceConfirmed = false;
        }

        // The count is across the account: it can never establish local connectivity or undo a local disconnect.
        bool connected = _hasLocalEvidence && !_locallyDisconnected && status.HostConnections > 0;
        _serviceConfirmed |= connected;
        DevTunnelHostConnectivity connectivity = _status.Connectivity switch
        {
            DevTunnelHostConnectivity.Stopped => DevTunnelHostConnectivity.Stopped,
            _ when !current => _status.Connectivity,
            _ when connected => DevTunnelHostConnectivity.Connected,
            DevTunnelHostConnectivity.Starting => DevTunnelHostConnectivity.Starting,
            _ => DevTunnelHostConnectivity.Disconnected,
        };

        Publish(
            _status with
            {
                Connectivity = connectivity,
                TunnelId = status.TunnelId.Length > 0 ? status.TunnelId : _status.TunnelId,
                Ports = ports,
                HostConnections = status.HostConnections,
                IsTakenOver = _locallyDisconnected && status.HostConnections > 0,
                Error = null,
            }
        );
    }

    private async Task ReconcileLoopAsync()
    {
        try
        {
            await Task.Delay(_options.OutputTimeout, _time, _stopping.Token).ConfigureAwait(false);
            while (!_stopping.IsCancellationRequested)
            {
                Post(() => _ = BeginQuery());
                await Task.Delay(_options.ReconcileInterval, _time, _stopping.Token)
                    .ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException)
        {
            // Deliberately not logged: the monitor is stopping.
        }
    }

    private async Task WatchExitAsync()
    {
        try
        {
            await _session.WaitForExitAsync(_stopping.Token).ConfigureAwait(false);
            Post(MarkStopped);
        }
        catch (OperationCanceledException)
        {
            // Deliberately not logged: the monitor is stopping.
        }
    }

    private void MarkStopped()
    {
        _revision++;
        Publish(
            _status with
            {
                Connectivity = DevTunnelHostConnectivity.Stopped,
                IsTakenOver = false,
            }
        );
        _ = _connected.TrySetException(
            new InvalidOperationException(_session.FailureReason ?? "The dev tunnel host stopped.")
        );
    }

    private void Publish(DevTunnelHostStatus next)
    {
        DevTunnelHostStatus stamped = next with { ObservedAt = _time.GetUtcNow() };
        DevTunnelHostStatus previous = _status;
        Volatile.Write(ref _status, stamped);
        if (
            previous.Connectivity == stamped.Connectivity
            && previous.IsTakenOver == stamped.IsTakenOver
            && previous.HostConnections == stamped.HostConnections
            && previous.TunnelId == stamped.TunnelId
            && previous.Error == stamped.Error
            && previous.Ports.Count == stamped.Ports.Count
        )
        {
            return;
        }

        try
        {
            StatusChanged?.Invoke(this, new DevTunnelHostStatusChangedEventArgs(previous, stamped));
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            _logger.LogError(exception, "A dev tunnel status handler failed.");
        }
    }
}
