using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Backup.Api.Security;

public static class ApiKeyAuthenticationServiceCollectionExtensions
{
    public static IServiceCollection AddApiKeyAuthentication(
        this IServiceCollection services,
        IConfiguration configuration
    )
    {
        services
            .AddOptions<ApiKeyAuthenticationOptions>()
            .Bind(configuration.GetSection(ApiKeyAuthenticationOptions.ConfigurationSection));

        return services;
    }
}
