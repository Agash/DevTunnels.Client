using System.Globalization;
using System.Text;
using Microsoft.Extensions.Logging;

namespace DevTunnels.Client.Internal;

internal sealed class DevTunnelCli(DevTunnelsClientOptions options, IProcessExecutor processExecutor, ILogger logger)
{
    public const int ResourceConflictsWithExistingExitCode = 1;
    public const int ResourceNotFoundExitCode = 2;

    public async Task<DevTunnelCliProbeResult> ProbeCandidateAsync(string candidate, CancellationToken cancellationToken)
    {
        try
        {
            ProcessExecutionResult result = await processExecutor.RunAsync(
                CreateSpec(candidate, ["--version", "--nologo"], useShellExecute: false, workingDirectory: null),
                cancellationToken).ConfigureAwait(false);

            string rawOutput = string.IsNullOrWhiteSpace(result.StandardOutput)
                ? result.StandardError.Trim()
                : result.StandardOutput.Trim();

            if (result.ExitCode != 0)
            {
                return new DevTunnelCliProbeResult(false, null, null, rawOutput, false, $"'{candidate} --version' exited with code {result.ExitCode}.");
            }

            if (!DevTunnelVersionParser.TryParse(rawOutput, out Version? version) || version is null)
            {
                return new DevTunnelCliProbeResult(false, candidate, null, rawOutput, false, "The devtunnel CLI returned a version string that could not be parsed.");
            }

            bool meetsMinimumVersion = version >= options.MinimumSupportedVersion;
            return new DevTunnelCliProbeResult(
                true,
                candidate,
                version,
                rawOutput,
                meetsMinimumVersion,
                meetsMinimumVersion ? null : $"Resolved devtunnel CLI version {version} is below the minimum supported version {options.MinimumSupportedVersion}.");
        }
        catch (Exception ex) when (ex is InvalidOperationException or OperationCanceledException or System.ComponentModel.Win32Exception)
        {
            if (logger.IsEnabled(LogLevel.Debug))
            {
                logger.LogDebug(ex, "Failed to probe devtunnel CLI candidate '{Candidate}'.", candidate);
            }

            return new DevTunnelCliProbeResult(false, null, null, null, false, ex.Message);
        }
    }

    public Task<DevTunnelCommandResult> RunRawAsync(IReadOnlyList<string> arguments, bool useShellExecute, CancellationToken cancellationToken) =>
        RunAsync(arguments, useShellExecute, workingDirectory: null, cancellationToken);

    public Task<DevTunnelCommandResult> CreateTunnelAsync(string tunnelId, DevTunnelOptions tunnelOptions, CancellationToken cancellationToken) =>
        RunAsync(
            new ArgsBuilder(["create"])
                .Add(tunnelId)
                .AddIfNotNull("--description", tunnelOptions.Description)
                .AddIfTrue("--allow-anonymous", tunnelOptions.AllowAnonymous)
                .AddValues("--labels", tunnelOptions.Labels)
                .Add("--json")
                .Add("--nologo")
                .Build(),
            false,
            null,
            cancellationToken);

    public Task<DevTunnelCommandResult> UpdateTunnelAsync(string tunnelId, DevTunnelOptions tunnelOptions, CancellationToken cancellationToken) =>
        RunAsync(
            new ArgsBuilder(["update", tunnelId])
                .AddIfNotNull("--description", tunnelOptions.Description)
                .AddValues("--add-labels", tunnelOptions.Labels)
                .Add("--json")
                .Add("--nologo")
                .Build(),
            false,
            null,
            cancellationToken);

    public Task<DevTunnelCommandResult> ShowTunnelAsync(string tunnelId, CancellationToken cancellationToken) =>
        RunAsync(["show", tunnelId, "--json", "--nologo"], false, null, cancellationToken);

    public Task<DevTunnelCommandResult> DeleteTunnelAsync(string tunnelId, CancellationToken cancellationToken) =>
        RunAsync(["delete", tunnelId, "--force", "--json", "--nologo"], false, null, cancellationToken);

    public Task<DevTunnelCommandResult> ListTunnelsAsync(CancellationToken cancellationToken) =>
        RunAsync(["list", "--json", "--nologo"], false, null, cancellationToken);

    public Task<DevTunnelCommandResult> ListPortsAsync(string tunnelId, CancellationToken cancellationToken) =>
        RunAsync(["port", "list", tunnelId, "--json", "--nologo"], false, null, cancellationToken);

    public Task<DevTunnelCommandResult> CreatePortAsync(string tunnelId, int portNumber, DevTunnelPortOptions portOptions, CancellationToken cancellationToken) =>
        RunAsync(
            new ArgsBuilder(["port", "create", tunnelId])
                .AddIfNotNull("--port-number", portNumber.ToString(CultureInfo.InvariantCulture))
                .AddIfNotNull("--protocol", portOptions.Protocol)
                .AddValues("--labels", portOptions.Labels)
                .Add("--json")
                .Add("--nologo")
                .Build(),
            false,
            null,
            cancellationToken);

    public Task<DevTunnelCommandResult> DeletePortAsync(string tunnelId, int portNumber, CancellationToken cancellationToken) =>
        RunAsync(["port", "delete", tunnelId, "--port-number", portNumber.ToString(CultureInfo.InvariantCulture), "--json", "--nologo"], false, null, cancellationToken);

    public Task<DevTunnelCommandResult> UpdatePortAsync(string tunnelId, int portNumber, DevTunnelPortOptions portOptions, CancellationToken cancellationToken) =>
        RunAsync(
            new ArgsBuilder(["port", "update", tunnelId])
                .AddIfNotNull("--port-number", portNumber.ToString(CultureInfo.InvariantCulture))
                .AddIfNotNull("--description", portOptions.Description)
                .AddValues("--add-labels", portOptions.Labels)
                .Add("--json")
                .Add("--nologo")
                .Build(),
            false,
            null,
            cancellationToken);

    public Task<DevTunnelCommandResult> GetAccessTokenAsync(string tunnelId, IReadOnlyList<string>? scopes, CancellationToken cancellationToken)
    {
        IReadOnlyList<string> effectiveScopes = scopes is { Count: > 0 } ? scopes : ["connect"];
        return RunAsync(
            new ArgsBuilder(["token", tunnelId])
                .AddIfNotNull("--scopes", string.Join(" ", effectiveScopes))
                .Add("--nologo")
                .Build(),
            false,
            null,
            cancellationToken);
    }

    public Task<DevTunnelCommandResult> ListAccessAsync(string tunnelId, int? portNumber, CancellationToken cancellationToken) =>
        RunAsync(
            new ArgsBuilder(["access", "list", tunnelId])
                .AddIfNotNull("--port-number", portNumber?.ToString(CultureInfo.InvariantCulture))
                .Add("--json")
                .Add("--nologo")
                .Build(),
            false,
            null,
            cancellationToken);

    public Task<DevTunnelCommandResult> ResetAccessAsync(string tunnelId, int? portNumber, CancellationToken cancellationToken) =>
        RunAsync(
            new ArgsBuilder(["access", "reset", tunnelId])
                .AddIfNotNull("--port-number", portNumber?.ToString(CultureInfo.InvariantCulture))
                .Add("--json")
                .Add("--nologo")
                .Build(),
            false,
            null,
            cancellationToken);

    public Task<DevTunnelCommandResult> CreateAccessAsync(string tunnelId, int? portNumber, bool anonymous, bool deny, CancellationToken cancellationToken) => !anonymous && !deny
            ? throw new ArgumentException("Either anonymous or deny must be true.", nameof(anonymous))
            : RunAsync(
            new ArgsBuilder(["access", "create", tunnelId])
                .AddIfNotNull("--port-number", portNumber?.ToString(CultureInfo.InvariantCulture))
                .AddIfTrue("--deny", deny)
                .AddIfTrue("--anonymous", anonymous)
                .Add("--json")
                .Add("--nologo")
                .Build(),
            false,
            null,
            cancellationToken);

    public Task<DevTunnelCommandResult> LoginMicrosoftAsync(CancellationToken cancellationToken) =>
        RunAsync(["user", "login", "--entra", "--json", "--nologo"], true, null, cancellationToken);

    public Task<DevTunnelCommandResult> LoginGitHubAsync(CancellationToken cancellationToken) =>
        RunAsync(["user", "login", "--github", "--json", "--nologo"], true, null, cancellationToken);

    public Task<DevTunnelCommandResult> LogoutAsync(CancellationToken cancellationToken) =>
        RunAsync(["user", "logout", "--json", "--nologo"], false, null, cancellationToken);

    public Task<IRunningProcess> StartHostAsync(DevTunnelHostStartOptions hostOptions, CancellationToken cancellationToken)
    {
        IReadOnlyList<string> args = new ArgsBuilder(["host"])
            .AddIfNotNull(null, hostOptions.TunnelId)
            .AddIfNotNull("-p", hostOptions.PortNumber?.ToString(CultureInfo.InvariantCulture))
            .Add("--nologo")
            .Build();

        return processExecutor.StartAsync(CreateSpec(GetResolvedCliPath(), args, false, hostOptions.WorkingDirectory), cancellationToken);
    }

    private async Task<DevTunnelCommandResult> RunAsync(IReadOnlyList<string> args, bool useShellExecute, string? workingDirectory, CancellationToken cancellationToken)
    {
        if (logger.IsEnabled(LogLevel.Trace))
        {
            logger.LogTrace(
                "Invoking devtunnel CLI{ShellExecuteInfo}: {CliPath} {Arguments}",
                useShellExecute ? " (UseShellExecute=true)" : string.Empty,
                GetResolvedCliPath(),
                string.Join(' ', args));
        }

        // Interactive shell-execute commands (login) are user-driven; skip timeout for those.
        CancellationTokenSource? timeoutCts = null;
        if (!useShellExecute && options.CommandTimeout > TimeSpan.Zero)
        {
            timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutCts.CancelAfter(options.CommandTimeout);
        }

        try
        {
            ProcessExecutionResult result = await processExecutor.RunAsync(
                CreateSpec(GetResolvedCliPath(), args, useShellExecute, workingDirectory),
                timeoutCts?.Token ?? cancellationToken).ConfigureAwait(false);

            return new DevTunnelCommandResult(result.ExitCode, result.StandardOutput, result.StandardError);
        }
        catch (OperationCanceledException) when (timeoutCts is not null && timeoutCts.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
        {
            throw new TimeoutException($"The devtunnel CLI command '{string.Join(' ', args)}' timed out after {options.CommandTimeout}.");
        }
        finally
        {
            timeoutCts?.Dispose();
        }
    }

    private static ProcessSpec CreateSpec(string fileName, IReadOnlyList<string> arguments, bool useShellExecute, string? workingDirectory) =>
        new(fileName, arguments, useShellExecute, workingDirectory, Encoding.UTF8, Encoding.UTF8);

    private string GetResolvedCliPath()
    {
        foreach (string candidate in DevTunnelsCliLocator.GetCandidateCommands(options))
        {
            // Skip absolute paths that don't exist on disk; allow relative names (PATH lookup)
            if (Path.IsPathRooted(candidate) && !File.Exists(candidate))
            {
                continue;
            }

            return candidate;
        }

        return DevTunnelsCliLocator.GetDefaultExecutableName();
    }

    private sealed class ArgsBuilder(IEnumerable<string> args)
    {
        private readonly List<string> _args = [.. args];

        public ArgsBuilder Add(string value)
        {
            _args.Add(value);
            return this;
        }

        public ArgsBuilder AddIfNotNull(string? key, string? value)
        {
            if (!string.IsNullOrWhiteSpace(value))
            {
                if (!string.IsNullOrWhiteSpace(key))
                {
                    _args.Add(key);
                }

                _args.Add(value);
            }

            return this;
        }

        public ArgsBuilder AddIfTrue(string flag, bool enabled)
        {
            if (enabled)
            {
                _args.Add(flag);
            }

            return this;
        }

        public ArgsBuilder AddValues(string key, IReadOnlyList<string>? values)
        {
            if (values is null)
            {
                return this;
            }

            foreach (string value in values)
            {
                _args.Add(key);
                _args.Add(value);
            }

            return this;
        }

        public IReadOnlyList<string> Build() => _args;
    }
}
