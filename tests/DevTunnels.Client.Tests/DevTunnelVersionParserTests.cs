using DevTunnels.Client.Internal.Cli;

namespace DevTunnels.Client.Tests;

[TestClass]
public sealed class DevTunnelVersionParserTests
{
    [TestMethod]
    public void TryParse_WithCommitSuffix_ReturnsParsedVersion()
    {
        bool success = DevTunnelVersionParser.TryParse(
            "Tunnel CLI version: 1.0.1435+d49a94cc24",
            out Version? version);

        Assert.IsTrue(success);
        Assert.AreEqual(new Version(1, 0, 1435), version);
    }
}
