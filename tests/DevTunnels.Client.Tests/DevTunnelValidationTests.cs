namespace DevTunnels.Client.Tests;

[TestClass]
public sealed class DevTunnelValidationTests
{
    [TestMethod]
    public void IsValidTunnelId_WithLowercaseHyphenatedId_ReturnsTrue() =>
        Assert.IsTrue(DevTunnelValidation.IsValidTunnelId("my-app-webhooks-01"));

    [TestMethod]
    public void IsValidTunnelId_WithUppercaseCharacters_ReturnsFalse() =>
        Assert.IsFalse(DevTunnelValidation.IsValidTunnelId("MyAppName"));

    [TestMethod]
    public void IsValidLabel_WithExpectedCharacters_ReturnsTrue() =>
        Assert.IsTrue(DevTunnelValidation.IsValidLabel("my-app_webhooks=prod"));

    [TestMethod]
    public void IsValidTunnelReference_WithClusterQualifiedId_ReturnsTrue() =>
        Assert.IsTrue(DevTunnelValidation.IsValidTunnelReference("my-app-webhooks-01.uks1"));

    [TestMethod]
    public void IsValidTunnelReference_WithBareId_ReturnsTrue() =>
        Assert.IsTrue(DevTunnelValidation.IsValidTunnelReference("my-app-webhooks-01"));

    [TestMethod]
    public void IsValidTunnelReference_WithTwoClusters_ReturnsFalse() =>
        Assert.IsFalse(DevTunnelValidation.IsValidTunnelReference("my-app.uks1.euw"));

    [TestMethod]
    public void IsValidTunnelId_WithClusterQualifiedId_ReturnsFalse() =>
        Assert.IsFalse(DevTunnelValidation.IsValidTunnelId("my-app-webhooks-01.uks1"));
}
