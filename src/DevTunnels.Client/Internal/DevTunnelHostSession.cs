using System.Text.RegularExpressions;
using DevTunnels.Client.Hosting;
using DevTunnels.Client.Internal.Process;
using Microsoft.Extensions.Logging;

namespace DevTunnels.Client.Internal;

internal sealed partial class DevTunnelHostSession : IDevTunnelHostSession
{
    private readonly IRunningProcess _runningProcess;
    private readonly TaskCompletionSource _readyTcs = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public DevTunnelHostSession(IRunningProcess runningProcess, DevTunnelHostStartOptions options, ILogger logger)
    {
        _runningProcess = runningProcess;
        State = DevTunnelHostState.Starting;
        TunnelId = options.TunnelId;
        _runningProcess.OutputReceived += HandleOutputReceived;
        _ = ObserveExitAsync(logger);
    }

    public DevTunnelHostState State { get; private set; }

    public string? TunnelId { get; private set; }

    public Uri? PublicUrl { get; private set; }

    public string? LastOutputLine { get; private set; }

    public string? FailureReason { get; private set; }

    public event EventHandler<DevTunnelHostOutputReceivedEventArgs>? OutputReceived;

    public async ValueTask WaitForReadyAsync(CancellationToken cancellationToken = default)
    {
        using CancellationTokenRegistration ctr = cancellationToken.Register(() => _readyTcs.TrySetCanceled(cancellationToken));
        await _readyTcs.Task.ConfigureAwait(false);
    }

    public async ValueTask WaitForExitAsync(CancellationToken cancellationToken = default) =>
        await _runningProcess.WaitForExitAsync(cancellationToken).ConfigureAwait(false);

    public async ValueTask StopAsync(CancellationToken cancellationToken = default)
    {
        await _runningProcess.StopAsync(cancellationToken).ConfigureAwait(false);
        if (State is not DevTunnelHostState.Failed)
        {
            State = DevTunnelHostState.Stopped;
        }
    }

    public async ValueTask DisposeAsync()
    {
        _runningProcess.OutputReceived -= HandleOutputReceived;
        await _runningProcess.DisposeAsync().ConfigureAwait(false);
    }

    private void HandleOutputReceived(bool isError, string line)
    {
        LastOutputLine = line;
        OutputReceived?.Invoke(this, new DevTunnelHostOutputReceivedEventArgs(isError, line));

        if (PublicUrl is null && TryExtractPublicUrl(line, out Uri? publicUrl))
        {
            PublicUrl = publicUrl;
            State = DevTunnelHostState.Running;
            _ = _readyTcs.TrySetResult();
        }

        if (TunnelId is null && TryExtractTunnelId(line, out string? tunnelId))
        {
            TunnelId = tunnelId;
            if (PublicUrl is not null)
            {
                State = DevTunnelHostState.Running;
                _ = _readyTcs.TrySetResult();
            }
        }
    }

    private async Task ObserveExitAsync(ILogger logger)
    {
        try
        {
            await _runningProcess.WaitForExitAsync(CancellationToken.None).ConfigureAwait(false);
            if (State == DevTunnelHostState.Starting)
            {
                State = DevTunnelHostState.Failed;
                FailureReason = LastOutputLine ?? "The devtunnel host process exited before becoming ready.";
                _ = _readyTcs.TrySetException(new InvalidOperationException(FailureReason));
                return;
            }

            if (State != DevTunnelHostState.Failed)
            {
                State = DevTunnelHostState.Stopped;
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "The devtunnel host session failed.");
            State = DevTunnelHostState.Failed;
            FailureReason = ex.Message;
            _ = _readyTcs.TrySetException(ex);
        }
    }

    private static bool TryExtractPublicUrl(string line, out Uri? publicUrl)
    {
        // Two-pass: prefer standard-port (HTTPS 443) URLs because many webhook providers
        // reject URLs with explicit non-standard ports (e.g. :5000).
        // The devtunnel CLI emits both forms on the "Connect via browser:" line:
        //   https://id.euw.devtunnels.ms:5000  (explicit port — don't use)
        //   https://id-5000.euw.devtunnels.ms  (port embedded in hostname — prefer this)
        Uri? fallback = null;
        foreach (Match match in TunnelUrlRegex().Matches(line))
        {
            string candidate = match.Groups[1].Value;

            // Skip the devtunnel network inspector URL — it's not the tunnel endpoint.
            if (candidate.Contains("-inspect.", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (!Uri.TryCreate(candidate, UriKind.Absolute, out Uri? parsed))
            {
                continue;
            }

            if (parsed.IsDefaultPort)
            {
                // Standard port (443 for HTTPS) — ideal for webhooks.
                publicUrl = parsed;
                return true;
            }

            fallback ??= parsed;
        }

        publicUrl = fallback;
        return fallback is not null;
    }

    private static bool TryExtractTunnelId(string line, out string? tunnelId)
    {
        Match idMatch = TunnelIdRegex().Match(line);
        if (idMatch.Success)
        {
            tunnelId = idMatch.Groups[1].Value;
            return true;
        }

        Match altMatch = TunnelIdAltRegex().Match(line);
        if (altMatch.Success)
        {
            tunnelId = altMatch.Groups[1].Value;
            return true;
        }

        tunnelId = null;
        return false;
    }

    [GeneratedRegex(@"(https?://[^\s,]+\.devtunnels\.ms[^\s,]*)", RegexOptions.IgnoreCase)]
    private static partial Regex TunnelUrlRegex();

    [GeneratedRegex(@"Tunnel\s+ID\s*:\s*(\S+)", RegexOptions.IgnoreCase)]
    private static partial Regex TunnelIdRegex();

    [GeneratedRegex(@"for tunnel:?\s+(\S+)", RegexOptions.IgnoreCase)]
    private static partial Regex TunnelIdAltRegex();
}
