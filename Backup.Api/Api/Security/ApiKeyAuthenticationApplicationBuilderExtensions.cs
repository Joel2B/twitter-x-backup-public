using Microsoft.AspNetCore.Builder;

namespace Backup.Api.Security;

public static class ApiKeyAuthenticationApplicationBuilderExtensions
{
    public static IApplicationBuilder UseApiKeyAuthentication(this IApplicationBuilder app) =>
        app.UseMiddleware<ApiKeyAuthenticationMiddleware>();
}
