using System.Text.Json.Serialization;

namespace DevTunnels.Client.Authentication;

/// <summary>
/// Supported interactive identity providers for the Azure Dev Tunnels CLI login flow.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<LoginProvider>))]
public enum LoginProvider
{
    /// <summary>
    /// Sign in with a Microsoft account.
    /// </summary>
    Microsoft,

    /// <summary>
    /// Sign in with a GitHub account.
    /// </summary>
    GitHub
}
