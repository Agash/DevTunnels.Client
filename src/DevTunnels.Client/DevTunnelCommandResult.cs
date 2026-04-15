namespace DevTunnels.Client;

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
