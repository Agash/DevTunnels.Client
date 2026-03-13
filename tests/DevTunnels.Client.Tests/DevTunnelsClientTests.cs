namespace DevTunnels.Client.Tests;

[TestClass]
public sealed class DevTunnelsClientTests
{
    [TestMethod]
    public async Task ProbeCliAsync_MissingExplicitPathOnly_ReturnsNotInstalled()
    {
        var options = new DevTunnelsClientOptions
        {
            CliPathOverride = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"), "devtunnel.exe"),
            IncludeKnownInstallLocations = false,
            IncludePathLookup = false
        };

        var client = new DevTunnelsClient(options);

        DevTunnelCliProbeResult result = await client.ProbeCliAsync();

        Assert.IsFalse(result.IsInstalled);
        Assert.IsFalse(result.MeetsMinimumVersion);
        Assert.IsNull(result.ResolvedPath);
    }
}
