using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using DevTunnels.Client.Access;
using DevTunnels.Client.Authentication;
using DevTunnels.Client.Hosting;
using DevTunnels.Client.Internal;
using DevTunnels.Client.Internal.Cli;
using DevTunnels.Client.Internal.Process;
using DevTunnels.Client.Ports;
using DevTunnels.Client.Tunnels;
using Microsoft.Extensions.Logging;

namespace DevTunnels.Client;

/// <summary>
/// High-level typed Azure Dev Tunnels client built on top of the local <c>devtunnel</c> CLI.
/// </summary>
public sealed class DevTunnelsClient : IDevTunnelsClient
{
    private readonly DevTunnelsClientOptions _options;
    private readonly ILogger<DevTunnelsClient> _logger;
    private readonly DevTunnelCli _cli;
    private readonly SemaphoreSlim _loginGate = new(1, 1);

    /// <summary>
    /// Initializes a new instance of the <see cref="DevTunnelsClient" /> class.
    /// </summary>
    /// <param name="options">Optional client options.</param>
    /// <param name="logger">Optional logger.</param>
    public DevTunnelsClient(DevTunnelsClientOptions? options = null, ILogger<DevTunnelsClient>? logger = null)
        : this(options, logger, processExecutor: null)
    {
    }

    internal DevTunnelsClient(
        DevTunnelsClientOptions? options,
        ILogger<DevTunnelsClient>? logger,
        IProcessExecutor? processExecutor)
    {
        _options = options ?? new DevTunnelsClientOptions();
        _logger = logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<DevTunnelsClient>.Instance;
        _cli = new DevTunnelCli(_options, processExecutor ?? new SystemProcessExecutor(), _logger);
    }

    /// <inheritdoc />
    public async ValueTask<DevTunnelCliProbeResult> ProbeCliAsync(CancellationToken cancellationToken = default)
    {
        foreach (string candidate in DevTunnelsCliLocator.GetCandidateCommands(_options))
        {
            if (LooksLikePath(candidate) && !File.Exists(candidate))
            {
                if (_logger.IsEnabled(LogLevel.Debug))
                {
                    _logger.LogDebug("Skipping devtunnel candidate path '{Candidate}' because the file does not exist.", candidate);
                }

                continue;
            }

            DevTunnelCliProbeResult result = await _cli.ProbeCandidateAsync(candidate, cancellationToken).ConfigureAwait(false);
            if (result.IsInstalled)
            {
                return result;
            }
        }

        return new DevTunnelCliProbeResult(
            false,
            null,
            null,
            null,
            false,
            "The devtunnel CLI could not be found in the configured override, known install locations, or PATH.");
    }

    /// <inheritdoc />
    public async ValueTask<Version> GetVersionAsync(CancellationToken cancellationToken = default)
    {
        DevTunnelCommandResult result = await ExecuteRawAsync(["--version", "--nologo"], cancellationToken: cancellationToken).ConfigureAwait(false);
        EnsureSuccess(result, "get the devtunnel CLI version");
        return !DevTunnelVersionParser.TryParse(result.StandardOutput, out Version? version) || version is null
            ? throw new DevTunnelsCliException("The devtunnel CLI version output could not be parsed.", result)
            : version;
    }

    /// <inheritdoc />
    public async ValueTask<DevTunnelLoginStatus> GetLoginStatusAsync(CancellationToken cancellationToken = default)
    {
        DevTunnelCommandResult result = await ExecuteRawAsync(["user", "show", "--json", "--nologo"], cancellationToken: cancellationToken).ConfigureAwait(false);
        EnsureSuccess(result, "get the current login status");
        return Deserialize(result, DevTunnelsJsonSerializerContext.Default.DevTunnelLoginStatus, "login status");
    }

    /// <inheritdoc />
    public async ValueTask<DevTunnelLoginStatus> EnsureLoggedInAsync(LoginProvider? provider = null, CancellationToken cancellationToken = default)
    {
        await _loginGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            LoginProvider selectedProvider = provider ?? _options.PreferredLoginProvider;
            DevTunnelLoginStatus status = await GetLoginStatusAsync(cancellationToken).ConfigureAwait(false);
            return status.IsLoggedIn && (status.Provider is null || status.Provider == selectedProvider)
                ? status
                : await LoginAsync(selectedProvider, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _ = _loginGate.Release();
        }
    }

    /// <inheritdoc />
    public async ValueTask<DevTunnelLoginStatus> LoginAsync(LoginProvider provider, CancellationToken cancellationToken = default)
    {
        DevTunnelCommandResult result = provider switch
        {
            LoginProvider.Microsoft => await _cli.LoginMicrosoftAsync(cancellationToken).ConfigureAwait(false),
            LoginProvider.GitHub => await _cli.LoginGitHubAsync(cancellationToken).ConfigureAwait(false),
            _ => throw new ArgumentOutOfRangeException(nameof(provider))
        };

        EnsureSuccess(result, $"start login using {provider}");
        return await GetLoginStatusAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async ValueTask LogoutAsync(CancellationToken cancellationToken = default)
    {
        DevTunnelCommandResult result = await _cli.LogoutAsync(cancellationToken).ConfigureAwait(false);
        EnsureSuccess(result, "log out of the devtunnel CLI");
    }

    /// <inheritdoc />
    public async ValueTask<DevTunnelList> ListTunnelsAsync(CancellationToken cancellationToken = default)
    {
        DevTunnelCommandResult result = await _cli.ListTunnelsAsync(cancellationToken).ConfigureAwait(false);
        EnsureSuccess(result, "list tunnels");
        return Deserialize(result, DevTunnelsJsonSerializerContext.Default.DevTunnelList, "tunnel list");
    }

    /// <inheritdoc />
    public async ValueTask<DevTunnelStatus> CreateOrUpdateTunnelAsync(string tunnelId, DevTunnelOptions options, CancellationToken cancellationToken = default)
    {
        ValidateTunnelInput(tunnelId, options);

        return await RunRetryAsync(async ct =>
        {
            DevTunnelCommandResult createResult = await _cli.CreateTunnelAsync(tunnelId, options, ct).ConfigureAwait(false);
            if (createResult.ExitCode == 0)
            {
                return Deserialize(createResult, DevTunnelsJsonSerializerContext.Default.DevTunnelStatus, "tunnel", "tunnel");
            }

            if (createResult.ExitCode == DevTunnelCli.ResourceConflictsWithExistingExitCode)
            {
                DevTunnelCommandResult updateResult = await _cli.UpdateTunnelAsync(tunnelId, options, ct).ConfigureAwait(false);
                EnsureSuccess(updateResult, $"update tunnel '{tunnelId}'");
                DevTunnelStatus updated = Deserialize(updateResult, DevTunnelsJsonSerializerContext.Default.DevTunnelStatus, "tunnel", "tunnel");

                DevTunnelCommandResult resetResult = await _cli.ResetAccessAsync(tunnelId, null, ct).ConfigureAwait(false);
                EnsureSuccess(resetResult, $"reset access for tunnel '{tunnelId}'");

                if (options.AllowAnonymous)
                {
                    DevTunnelCommandResult accessResult = await _cli.CreateAccessAsync(tunnelId, null, anonymous: true, deny: false, ct).ConfigureAwait(false);
                    EnsureSuccess(accessResult, $"allow anonymous access for tunnel '{tunnelId}'");
                }

                return updated;
            }

            throw new DevTunnelsCliException($"Failed to create tunnel '{tunnelId}'.", createResult);
        }, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async ValueTask<DevTunnelStatus> GetTunnelAsync(string tunnelId, CancellationToken cancellationToken = default)
    {
        DevTunnelCommandResult result = await _cli.ShowTunnelAsync(tunnelId, cancellationToken).ConfigureAwait(false);
        EnsureSuccess(result, $"get tunnel '{tunnelId}'");
        return Deserialize(result, DevTunnelsJsonSerializerContext.Default.DevTunnelStatus, "tunnel", "tunnel");
    }

    /// <inheritdoc />
    public async ValueTask<DevTunnelDeleteResult> DeleteTunnelAsync(string tunnelId, CancellationToken cancellationToken = default)
    {
        DevTunnelCommandResult result = await _cli.DeleteTunnelAsync(tunnelId, cancellationToken).ConfigureAwait(false);
        EnsureSuccess(result, $"delete tunnel '{tunnelId}'");
        return Deserialize(result, DevTunnelsJsonSerializerContext.Default.DevTunnelDeleteResult, "delete tunnel result");
    }

    /// <inheritdoc />
    public async ValueTask<DevTunnelPortList> GetPortListAsync(string tunnelId, CancellationToken cancellationToken = default)
    {
        DevTunnelCommandResult result = await _cli.ListPortsAsync(tunnelId, cancellationToken).ConfigureAwait(false);
        EnsureSuccess(result, $"list ports for tunnel '{tunnelId}'");
        return Deserialize(result, DevTunnelsJsonSerializerContext.Default.DevTunnelPortList, "port list");
    }

    /// <inheritdoc />
    public async ValueTask<DevTunnelPortStatus> CreateOrReplacePortAsync(string tunnelId, int portNumber, DevTunnelPortOptions options, CancellationToken cancellationToken = default)
    {
        ValidatePortInput(tunnelId, portNumber, options);

        return await RunRetryAsync(async ct =>
        {
            DevTunnelCommandResult createResult = await _cli.CreatePortAsync(tunnelId, portNumber, options, ct).ConfigureAwait(false);
            if (createResult.ExitCode == DevTunnelCli.ResourceConflictsWithExistingExitCode)
            {
                DevTunnelCommandResult deleteResult = await _cli.DeletePortAsync(tunnelId, portNumber, ct).ConfigureAwait(false);
                EnsureSuccess(deleteResult, $"delete conflicting port '{portNumber}' on tunnel '{tunnelId}'");
                createResult = await _cli.CreatePortAsync(tunnelId, portNumber, options, ct).ConfigureAwait(false);
            }

            EnsureSuccess(createResult, $"create port '{portNumber}' on tunnel '{tunnelId}'");
            DevTunnelPortStatus status = Deserialize(createResult, DevTunnelsJsonSerializerContext.Default.DevTunnelPortStatus, "port status");

            if (options.AllowAnonymous.HasValue)
            {
                DevTunnelCommandResult accessResult = await _cli.CreateAccessAsync(
                    tunnelId,
                    portNumber,
                    anonymous: true,
                    deny: !options.AllowAnonymous.Value,
                    ct).ConfigureAwait(false);

                EnsureSuccess(accessResult, $"set anonymous access for port '{portNumber}' on tunnel '{tunnelId}'");
            }

            return status;
        }, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async ValueTask<DevTunnelPortDeleteResult> DeletePortAsync(string tunnelId, int portNumber, CancellationToken cancellationToken = default)
    {
        DevTunnelCommandResult result = await _cli.DeletePortAsync(tunnelId, portNumber, cancellationToken).ConfigureAwait(false);
        EnsureSuccess(result, $"delete port '{portNumber}' on tunnel '{tunnelId}'");
        return Deserialize(result, DevTunnelsJsonSerializerContext.Default.DevTunnelPortDeleteResult, "delete port result");
    }

    /// <inheritdoc />
    public async ValueTask<DevTunnelAccessStatus> GetAccessAsync(string tunnelId, int? portNumber = null, CancellationToken cancellationToken = default)
    {
        DevTunnelCommandResult result = await _cli.ListAccessAsync(tunnelId, portNumber, cancellationToken).ConfigureAwait(false);
        EnsureSuccess(result, $"list access for tunnel '{tunnelId}'");
        return Deserialize(result, DevTunnelsJsonSerializerContext.Default.DevTunnelAccessStatus, "access status");
    }

    /// <inheritdoc />
    public async ValueTask<DevTunnelAccessStatus> ResetAccessAsync(string tunnelId, int? portNumber = null, CancellationToken cancellationToken = default)
    {
        DevTunnelCommandResult result = await _cli.ResetAccessAsync(tunnelId, portNumber, cancellationToken).ConfigureAwait(false);
        EnsureSuccess(result, $"reset access for tunnel '{tunnelId}'");
        return Deserialize(result, DevTunnelsJsonSerializerContext.Default.DevTunnelAccessStatus, "access status");
    }

    /// <inheritdoc />
    public async ValueTask<DevTunnelAccessStatus> CreateAccessAsync(string tunnelId, bool anonymous, bool deny = false, int? portNumber = null, CancellationToken cancellationToken = default)
    {
        DevTunnelCommandResult result = await _cli.CreateAccessAsync(tunnelId, portNumber, anonymous, deny, cancellationToken).ConfigureAwait(false);
        EnsureSuccess(result, $"create access entry for tunnel '{tunnelId}'");
        return Deserialize(result, DevTunnelsJsonSerializerContext.Default.DevTunnelAccessStatus, "access status");
    }

    /// <inheritdoc />
    public async ValueTask<DevTunnelPortStatus> UpdatePortAsync(string tunnelId, int portNumber, DevTunnelPortOptions options, CancellationToken cancellationToken = default)
    {
        ValidatePortInput(tunnelId, portNumber, options);
        DevTunnelCommandResult result = await _cli.UpdatePortAsync(tunnelId, portNumber, options, cancellationToken).ConfigureAwait(false);
        EnsureSuccess(result, $"update port '{portNumber}' on tunnel '{tunnelId}'");
        return Deserialize(result, DevTunnelsJsonSerializerContext.Default.DevTunnelPortStatus, "update port status");
    }

    /// <inheritdoc />
    public async ValueTask<string> GetAccessTokenAsync(string tunnelId, IReadOnlyList<string>? scopes = null, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tunnelId);
        DevTunnelCommandResult result = await _cli.GetAccessTokenAsync(tunnelId, scopes, cancellationToken).ConfigureAwait(false);
        EnsureSuccess(result, $"issue access token for tunnel '{tunnelId}'");

        foreach (string line in result.StandardOutput.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            string trimmed = line.Trim();
            if (trimmed.StartsWith("Token:", StringComparison.OrdinalIgnoreCase))
            {
                string token = trimmed["Token:".Length..].Trim();
                if (!string.IsNullOrEmpty(token))
                {
                    return token;
                }
            }
        }

        throw new DevTunnelsCliException($"Could not parse access token from devtunnel CLI output for tunnel '{tunnelId}'.", result);
    }

    /// <inheritdoc />
    public async ValueTask<DevTunnelCommandResult> ExecuteRawAsync(IReadOnlyList<string> arguments, bool useShellExecute = false, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(arguments);
        return await _cli.RunRawAsync(arguments, useShellExecute, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async ValueTask<IDevTunnelHostSession> StartHostSessionAsync(DevTunnelHostStartOptions options, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(options);

        if (string.IsNullOrWhiteSpace(options.TunnelId) && !options.PortNumber.HasValue)
        {
            throw new ArgumentException("A host session requires either a tunnel ID or a local port number.", nameof(options));
        }

        if (options.TunnelId is { Length: > 0 } tunnelId && !DevTunnelValidation.IsValidTunnelId(tunnelId))
        {
            throw new ArgumentException($"Tunnel ID '{tunnelId}' is invalid.", nameof(options));
        }

        IRunningProcess runningProcess = await _cli.StartHostAsync(options, cancellationToken).ConfigureAwait(false);
        return new DevTunnelHostSession(runningProcess, options, _logger);
    }

    private async ValueTask<T> RunRetryAsync<T>(Func<CancellationToken, ValueTask<T>> action, CancellationToken cancellationToken)
    {
        Exception? lastException = null;

        for (int attempt = 1; attempt <= _options.MaxCliAttempts; attempt++)
        {
            try
            {
                return await action(cancellationToken).ConfigureAwait(false);
            }
            catch (DevTunnelsCliException ex) when (attempt < _options.MaxCliAttempts)
            {
                lastException = ex;
                _logger.LogWarning(ex, "Retryable devtunnel operation failed on attempt {Attempt} of {MaxAttempts}.", attempt, _options.MaxCliAttempts);
                await Task.Delay(_options.CliRetryDelay, cancellationToken).ConfigureAwait(false);
            }
        }

        throw lastException ?? new InvalidOperationException("Retry loop ended without an exception.");
    }

    private static T Deserialize<T>(DevTunnelCommandResult result, JsonTypeInfo<T> typeInfo, string operationDescription, string? propertyName = null)
    {
        string payload = result.StandardOutput.Trim();
        if (string.IsNullOrWhiteSpace(payload))
        {
            throw new DevTunnelsCliException($"The devtunnel CLI returned empty output while attempting to {operationDescription}.", result);
        }

        try
        {
            if (!string.IsNullOrWhiteSpace(propertyName))
            {
                using JsonDocument doc = JsonDocument.Parse(payload);
                payload = doc.RootElement.GetProperty(propertyName).GetRawText();
            }

            T? value = JsonSerializer.Deserialize(payload, typeInfo);
            return value ?? throw new DevTunnelsCliException($"The devtunnel CLI returned JSON that could not be deserialized while attempting to {operationDescription}.", result);
        }
        catch (JsonException ex)
        {
            throw new DevTunnelsCliException($"The devtunnel CLI returned invalid JSON while attempting to {operationDescription}.", result, ex);
        }
    }

    private static void EnsureSuccess(DevTunnelCommandResult result, string action)
    {
        if (result.ExitCode != 0)
        {
            throw new DevTunnelsCliException($"The devtunnel CLI failed to {action}.", result);
        }
    }

    private static void ValidateTunnelInput(string tunnelId, DevTunnelOptions options)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tunnelId);
        ArgumentNullException.ThrowIfNull(options);

        if (!DevTunnelValidation.IsValidTunnelId(tunnelId))
        {
            throw new ArgumentException($"Tunnel ID '{tunnelId}' is invalid.", nameof(tunnelId));
        }

        foreach (string label in options.Labels)
        {
            if (!DevTunnelValidation.IsValidLabel(label))
            {
                throw new ArgumentException($"Label '{label}' is invalid.", nameof(options));
            }
        }
    }

    private static void ValidatePortInput(string tunnelId, int portNumber, DevTunnelPortOptions options)
    {
        ValidateTunnelInput(tunnelId, new DevTunnelOptions());
        ArgumentNullException.ThrowIfNull(options);

        if (portNumber is <= 0 or > 65535)
        {
            throw new ArgumentOutOfRangeException(nameof(portNumber), "Port numbers must be between 1 and 65535.");
        }

        foreach (string label in options.Labels)
        {
            if (!DevTunnelValidation.IsValidLabel(label))
            {
                throw new ArgumentException($"Label '{label}' is invalid.", nameof(options));
            }
        }
    }

    private static bool LooksLikePath(string candidate) =>
        Path.IsPathRooted(candidate) ||
        candidate.Contains(Path.DirectorySeparatorChar, StringComparison.Ordinal) ||
        candidate.Contains(Path.AltDirectorySeparatorChar, StringComparison.Ordinal);
}
