using DevTunnels.Client.Internal;

namespace DevTunnels.Client.Tests;

[TestClass]
public sealed class DevTunnelHostOutputParserTests
{
    [TestMethod]
    public void Parse_LabelsAndValuesOnSeparateLines_ReportsThePort()
    {
        DevTunnelHostOutputParser parser = new("sw-1");
        Assert.AreEqual(DevTunnelHostOutputKind.None, parser.Parse("Hosting port: 7770").Kind);
        DevTunnelHostOutput output = parser.Parse(
            "Connect via browser: https://abc-7770.uks1.devtunnels.ms/"
        );
        Assert.AreEqual(DevTunnelHostOutputKind.Port, output.Kind);
        Assert.AreEqual(7770, output.Port);
        Assert.AreEqual(new Uri("https://abc-7770.uks1.devtunnels.ms/"), output.PortUri);
    }

    [TestMethod]
    public void Parse_InspectUrl_IsNotThePort()
    {
        DevTunnelHostOutputParser parser = new("sw-1");
        _ = parser.Parse("Hosting port: 7770");
        Assert.AreEqual(
            DevTunnelHostOutputKind.None,
            parser
                .Parse("Inspect network activity: https://abc-7770-inspect.uks1.devtunnels.ms")
                .Kind
        );
    }

    [TestMethod]
    [DataRow("Ready to accept connections for tunnel: sw-1")]
    [DataRow("Ready to accept connections for tunnel: sw-1.uks1")]
    [DataRow("Ready to accept connections.")]
    public void Parse_Ready_ForThisTunnel(string line) =>
        Assert.AreEqual(
            DevTunnelHostOutputKind.Ready,
            new DevTunnelHostOutputParser("sw-1").Parse(line).Kind
        );

    [TestMethod]
    public void Parse_ReadyForAnotherTunnel_IsUnrecognized() =>
        Assert.AreEqual(
            DevTunnelHostOutputKind.Unrecognized,
            new DevTunnelHostOutputParser("sw-1")
                .Parse("Ready to accept connections for tunnel: other")
                .Kind
        );

    [TestMethod]
    [DataRow("Connection to host tunnel relay closed.", (int)DevTunnelHostOutputKind.Disconnected)]
    [DataRow(
        "\u001b[31mConnection to host tunnel relay lost\u001b[0m",
        (int)DevTunnelHostOutputKind.Disconnected
    )]
    [DataRow("Connection to host tunnel relay restored.", (int)DevTunnelHostOutputKind.Connected)]
    public void Parse_Relay_ReportsTheState(string line, int expected) =>
        Assert.AreEqual(
            (DevTunnelHostOutputKind)expected,
            new DevTunnelHostOutputParser("sw-1").Parse(line).Kind
        );

    [TestMethod]
    public void Parse_DisconnectAfterAnAbandonedLabel_IsNotLost()
    {
        DevTunnelHostOutputParser parser = new("sw-1");
        _ = parser.Parse("Inspect network activity:");
        Assert.AreEqual(
            DevTunnelHostOutputKind.Disconnected,
            parser.Parse("Connection to host tunnel relay closed.").Kind
        );
    }

    [TestMethod]
    public void SplitOutput_TwoMessagesOnOneLine_SplitsThem()
    {
        string[] parts = DevTunnelHostOutputParser.SplitOutput(
            "Connection to host tunnel relay restored.; \u001b[31mConnection to host tunnel relay closed.\u001b[0m"
        );
        Assert.HasCount(2, parts);
    }
}
