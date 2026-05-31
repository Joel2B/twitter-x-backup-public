using Backup.Application.Config;
using Backup.Application.Config.Models;
using Backup.Infrastructure.Models.Config.Api;

namespace Backup.Infrastructure.Models.Config.Request;

public static class RequestMerge
{
    public static Request? Build(IReadOnlyDictionary<string, ApiConfig> requests, string key)
    {
        IReadOnlyDictionary<string, ApiRequestBuildSource> sources = requests.ToDictionary(
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

        ApiRequestBuildResult? built = new ApiRequestBuildService().Build(sources, key);

        if (built is null)
            return null;

        return new Request
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
}
