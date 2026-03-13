namespace DevTunnels.Client.Tests;

[TestClass]
public sealed class DevTunnelValidationTests
{
    [TestMethod]
    public void IsValidTunnelId_WithLowercaseHyphenatedId_ReturnsTrue() => Assert.IsTrue(DevTunnelValidation.IsValidTunnelId("my-app-webhooks-01"));

    [TestMethod]
    public void IsValidTunnelId_WithUppercaseCharacters_ReturnsFalse() => Assert.IsFalse(DevTunnelValidation.IsValidTunnelId("MyAppName"));

    [TestMethod]
    public void IsValidLabel_WithExpectedCharacters_ReturnsTrue() => Assert.IsTrue(DevTunnelValidation.IsValidLabel("my-app_webhooks=prod"));
}
