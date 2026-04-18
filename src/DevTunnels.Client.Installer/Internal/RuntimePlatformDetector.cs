namespace DevTunnels.Client.Installer.Internal;

internal sealed class RuntimePlatformDetector : IPlatformDetector
{
    public bool IsWindows() => OperatingSystem.IsWindows();
    public bool IsMacOS() => OperatingSystem.IsMacOS();
}
