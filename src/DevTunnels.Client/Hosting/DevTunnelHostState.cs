namespace DevTunnels.Client;

/// <summary>
/// Represents the lifecycle state of a running host session.
/// </summary>
public enum DevTunnelHostState
{
    /// <summary>The host process has been created but is not ready yet.</summary>
    Starting,

    /// <summary>The host process is running and produced a usable public URL or tunnel identity.</summary>
    Running,

    /// <summary>The host process has stopped cleanly.</summary>
    Stopped,

    /// <summary>The host process failed or exited unexpectedly.</summary>
    Failed
}
