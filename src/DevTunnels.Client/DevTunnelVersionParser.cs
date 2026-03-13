namespace DevTunnels.Client;

internal static class DevTunnelVersionParser
{
    private const string VersionPrefix = "Tunnel CLI version:";

    public static bool TryParse(string? rawOutput, out Version? version)
    {
        version = null;

        if (string.IsNullOrWhiteSpace(rawOutput))
        {
            return false;
        }

        string? versionLine = rawOutput
            .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .FirstOrDefault(static line => line.StartsWith(VersionPrefix, StringComparison.OrdinalIgnoreCase));

        string versionText = versionLine is null
            ? rawOutput.Trim()
            : versionLine[VersionPrefix.Length..].Trim();

        int plusIndex = versionText.IndexOf('+', StringComparison.Ordinal);
        if (plusIndex >= 0)
        {
            versionText = versionText[..plusIndex];
        }

        return Version.TryParse(versionText, out version);
    }
}
