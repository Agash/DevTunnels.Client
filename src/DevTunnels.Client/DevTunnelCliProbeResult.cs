namespace DevTunnels.Client;

/// <summary>
/// Describes the outcome of probing the local machine for a <c>devtunnel</c> CLI installation.
/// </summary>
/// <param name="IsInstalled">Whether a CLI executable was located and successfully invoked.</param>
/// <param name="ResolvedPath">The resolved executable path or command name that was used.</param>
/// <param name="Version">The parsed CLI version when available.</param>
/// <param name="RawOutput">The raw CLI output captured during probing.</param>
/// <param name="MeetsMinimumVersion">Whether the resolved version satisfies the configured minimum.</param>
/// <param name="FailureReason">A human-readable explanation when probing does not succeed.</param>
public sealed record DevTunnelCliProbeResult(
    bool IsInstalled,
    string? ResolvedPath,
    Version? Version,
    string? RawOutput,
    bool MeetsMinimumVersion,
    string? FailureReason);

/// <summary>
/// Represents the raw result of executing a single <c>devtunnel</c> CLI command.
/// </summary>
/// <param name="ExitCode">The process exit code.</param>
/// <param name="StandardOutput">Captured standard output.</param>
/// <param name="StandardError">Captured standard error.</param>
public sealed record DevTunnelCommandResult(
    int ExitCode,
    string StandardOutput,
    string StandardError);
