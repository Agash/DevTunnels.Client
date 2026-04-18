namespace DevTunnels.Client.Installer.Internal;

internal interface IPlatformDetector
{
    bool IsWindows();
    bool IsMacOS();
}
