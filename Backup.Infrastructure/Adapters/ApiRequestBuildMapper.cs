using Backup.Application.Config.Models;
using Backup.Infrastructure.Models.Config.Api;
using Backup.Infrastructure.Models.Config.Request;

namespace Backup.Infrastructure.Adapters;

internal static class ApiRequestBuildMapper
{
    internal static IReadOnlyDictionary<string, ApiRequestBuildSource> ToSources(
        IReadOnlyDictionary<string, ApiConfig> requests
    ) =>
        requests.ToDictionary(
            kvp => kvp.Key,
            kvp =>
            {
                ApiConfig api = kvp.Value;
                return new ApiRequestBuildSource
                {
                    Enabled = api.Enabled,
                    Url = api.Request.Url,
                    Variables = api.Request.Query.Variables.ToDictionary(
                        entry => entry.Key,
                        entry => entry.Value
                    ),
                    Features = api.Request.Query.Features.ToDictionary(
                        entry => entry.Key,
                        entry => entry.Value
                    ),
                    FieldToggles = api.Request.Query.FieldToggles.ToDictionary(
                        entry => entry.Key,
                        entry => entry.Value
                    ),
                    Headers = api.Request.Headers.ToDictionary(entry => entry.Key, entry => entry.Value),
                };
            }
        );

    internal static Request ToRequest(ApiRequestBuildResult built) =>
        new()
        {
            Url = built.Url,
            Query = new Query
            {
                Variables = built.Variables.ToDictionary(entry => entry.Key, entry => entry.Value),
                Features = built.Features.ToDictionary(entry => entry.Key, entry => entry.Value),
                FieldToggles = built.FieldToggles.ToDictionary(entry => entry.Key, entry => entry.Value),
            },
            Headers = built.Headers.ToDictionary(entry => entry.Key, entry => entry.Value),
        };
}
