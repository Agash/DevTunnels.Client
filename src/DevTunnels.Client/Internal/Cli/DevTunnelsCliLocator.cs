namespace DevTunnels.Client.Internal.Cli;

internal static class DevTunnelsCliLocator
{
    private const string CliPathOverrideEnvironmentVariable = "DEVTUNNEL_CLI_PATH";

    public static IReadOnlyList<string> GetCandidateCommands(DevTunnelsClientOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        StringComparer comparer = OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;
        var candidates = new List<string>();
        var seen = new HashSet<string>(comparer);

        AddIfMeaningful(options.CliPathOverride);
        AddIfMeaningful(Environment.GetEnvironmentVariable(CliPathOverrideEnvironmentVariable));

        if (options.IncludeKnownInstallLocations)
        {
            foreach (string candidate in GetKnownInstallLocations())
            {
                AddIfMeaningful(candidate);
            }
        }

        if (options.IncludePathLookup)
        {
            AddIfMeaningful(GetDefaultExecutableName());
        }

        return candidates;

        void AddIfMeaningful(string? candidate)
        {
            if (string.IsNullOrWhiteSpace(candidate))
            {
                return;
            }

            string normalized = candidate.Trim();
            if (seen.Add(normalized))
            {
                candidates.Add(normalized);
            }
        }
    }

    public static string GetDefaultExecutableName() => OperatingSystem.IsWindows() ? "devtunnel.exe" : "devtunnel";

    private static IEnumerable<string> GetKnownInstallLocations()
    {
        string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

        if (OperatingSystem.IsWindows())
        {
            string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);

            yield return Path.Combine(localAppData, "Microsoft", "DevTunnels", "devtunnel.exe");
            yield return Path.Combine(home, ".devtunnels", "bin", "devtunnel.exe");
            yield return Path.Combine(home, "bin", "devtunnel.exe");
            yield break;
        }

        yield return Path.Combine(home, "bin", "devtunnel");
        yield return Path.Combine(home, ".local", "bin", "devtunnel");
        yield return "/usr/local/bin/devtunnel";
        yield return "/opt/homebrew/bin/devtunnel";
    }
}
