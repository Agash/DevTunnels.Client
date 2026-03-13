namespace DevTunnels.Client.Tests;

[TestClass]
public sealed class DevTunnelsCliLocatorTests
{
    [TestMethod]
    public void GetCandidateCommands_WithExplicitOverride_PutsOverrideFirst()
    {
        var options = new DevTunnelsClientOptions
        {
            CliPathOverride = @"C:\tools\devtunnel.exe"
        };

        IReadOnlyList<string> candidates = DevTunnelsCliLocator.GetCandidateCommands(options);

        Assert.AreEqual(@"C:\tools\devtunnel.exe", candidates[0]);
    }
}
