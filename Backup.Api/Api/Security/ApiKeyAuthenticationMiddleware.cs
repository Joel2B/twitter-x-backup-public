using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Backup.Api.Security;

public sealed class ApiKeyAuthenticationMiddleware(
    RequestDelegate next,
    IOptions<ApiKeyAuthenticationOptions> options,
    ILogger<ApiKeyAuthenticationMiddleware> logger
)
{
    private readonly RequestDelegate _next = next;
    private readonly ApiKeyAuthenticationOptions _options = options.Value;
    private readonly ILogger<ApiKeyAuthenticationMiddleware> _logger = logger;

    public async Task InvokeAsync(HttpContext context)
    {
        if (!_options.Enabled)
        {
            await _next(context);
            return;
        }

        if (!context.Request.Path.StartsWithSegments("/api", StringComparison.OrdinalIgnoreCase))
        {
            await _next(context);
            return;
        }

        if (string.IsNullOrWhiteSpace(_options.ApiKey))
        {
            _logger.LogError(
                "API key auth is enabled but no key is configured in section {section}.",
                ApiKeyAuthenticationOptions.ConfigurationSection
            );

            context.Response.StatusCode = StatusCodes.Status500InternalServerError;
            await context.Response.WriteAsJsonAsync(
                new { error = "API key authentication is misconfigured." }
            );
            return;
        }

        string headerName = string.IsNullOrWhiteSpace(_options.HeaderName)
            ? "X-Api-Key"
            : _options.HeaderName;

        if (
            !context.Request.Headers.TryGetValue(headerName, out var provided)
            || provided.Count == 0
            || !string.Equals(provided[0], _options.ApiKey, StringComparison.Ordinal)
        )
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            await context.Response.WriteAsJsonAsync(new { error = "Invalid or missing API key." });
            return;
        }

        await _next(context);
    }
}
