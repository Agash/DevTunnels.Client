namespace DevTunnels.Client.Hosting;

/// <summary>
/// Provides data for the <see cref="IDevTunnelHostSession.OutputReceived"/> event.
/// </summary>
/// <param name="IsError">
/// <see langword="true"/> if the line was written to standard error; <see langword="false"/> for standard output.
/// </param>
/// <param name="Line">The raw text line emitted by the host process.</param>
public sealed record DevTunnelHostOutputReceivedEventArgs(bool IsError, string Line);
