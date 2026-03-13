using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DevTunnels.Client.DependencyInjection;

/// <summary>
/// Adds dependency injection helpers for <see cref="DevTunnelsClient" />.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers <see cref="IDevTunnelsClient" /> and optional in-memory configuration.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configure">Optional configuration callback.</param>
    /// <returns>The same service collection for chaining.</returns>
    public static IServiceCollection AddDevTunnelsClient(
        this IServiceCollection services,
        Action<DevTunnelsClientOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        _ = services.AddOptions<DevTunnelsClientOptions>();

        if (configure is not null)
        {
            _ = services.Configure(configure);
        }

        return services.AddSingleton<IDevTunnelsClient>(sp =>
        {
            DevTunnelsClientOptions options = sp.GetRequiredService<IOptions<DevTunnelsClientOptions>>().Value;
            ILogger<DevTunnelsClient>? logger = sp.GetService<ILogger<DevTunnelsClient>>();
            return new DevTunnelsClient(options, logger);
        });
    }

    /// <summary>
    /// Registers <see cref="IDevTunnelsClient" /> and binds options from configuration.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">The configuration section or root to bind.</param>
    /// <returns>The same service collection for chaining.</returns>
    public static IServiceCollection AddDevTunnelsClient(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        _ = services.Configure<DevTunnelsClientOptions>(configuration);
        return services.AddDevTunnelsClient();
    }
}
