using System.Text.Json.Serialization;

namespace DevTunnels.Client;

/// <summary>
/// Source-generated JSON serializer context for all DevTunnels model types.
/// Enables AOT-compatible JSON deserialization without runtime reflection.
/// </summary>
[JsonSerializable(typeof(DevTunnelLoginStatus))]
[JsonSerializable(typeof(DevTunnelStatus))]
[JsonSerializable(typeof(DevTunnelPortList))]
[JsonSerializable(typeof(DevTunnelPort))]
[JsonSerializable(typeof(DevTunnelPortStatus))]
[JsonSerializable(typeof(DevTunnelPortDeleteResult))]
[JsonSerializable(typeof(DevTunnelDeleteResult))]
[JsonSerializable(typeof(DevTunnelAccessStatus))]
[JsonSerializable(typeof(DevTunnelAccessEntry))]
[JsonSerializable(typeof(DevTunnelList))]
[JsonSourceGenerationOptions(
    PropertyNameCaseInsensitive = true,
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    NumberHandling = JsonNumberHandling.AllowReadingFromString)]
internal sealed partial class DevTunnelsJsonSerializerContext : JsonSerializerContext
{
}
