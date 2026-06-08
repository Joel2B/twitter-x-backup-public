using Backup.Api.Errors;
using Backup.Infrastructure.Core.Abstractions.Config;
using Backup.Infrastructure.Models.Config.Api;

namespace Backup.Api.Services;

public sealed class ConfigContextResolver(IAppConfigService configService)
{
    private readonly IAppConfigService _configService = configService;

    public AppConfigSnapshot GetSnapshot() => _configService.GetSnapshot();

    public AppConfigSnapshot RefreshSnapshot() => _configService.Refresh();

    public UsersContext GetRequiredUsersContext(string userId)
    {
        UsersContext source = GetRequiredUserContext(GetSnapshot(), userId);

        return new UsersContext
        {
            UserId = source.UserId,
            Api = source.Api.ToDictionary(
                entry => entry.Key,
                entry => new ApiConfig
                {
                    Id = entry.Value.Id,
                    Enabled = entry.Value.Enabled,
                    Request = entry.Value.Request.Clone(),
                },
                StringComparer.Ordinal
            ),
        };
    }

    public ApiContext GetRequiredApiContext(string userId, string sourceId)
    {
        AppConfigSnapshot snapshot = GetSnapshot();
        UsersContext user = GetRequiredUserContext(snapshot, userId);

        if (!user.Api.TryGetValue(sourceId, out ApiConfig? api))
            throw new ApiException($"source '{sourceId}' was not found for user '{userId}'.");

        if (
            !snapshot.Value.Fetch.TryGetValue(
                sourceId,
                out Backup.Infrastructure.Models.Config.FetchItem? fetch
            )
        )
        {
            throw new ApiException($"fetch configuration for source '{sourceId}' was not found.");
        }

        return new ApiContext
        {
            Id = api.Id,
            UserId = userId,
            Count = fetch.Count,
            Request = api.Request.Clone(),
        };
    }

    private static UsersContext GetRequiredUserContext(AppConfigSnapshot snapshot, string userId)
    {
        UsersContext? user = snapshot.Value.UsersContext.FirstOrDefault(context =>
            string.Equals(context.UserId, userId, StringComparison.Ordinal)
        );

        if (user is not null)
            return user;

        throw new ApiException($"user '{userId}' was not found in configuration.");
    }
}
