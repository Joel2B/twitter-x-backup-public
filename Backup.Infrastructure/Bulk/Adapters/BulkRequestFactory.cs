using Backup.Infrastructure.Bulk.Abstractions.Services;
using Backup.Infrastructure.Models.Config.Api;
using Backup.Infrastructure.Models.Config.Request;

namespace Backup.Infrastructure.Bulk.Adapters;

public sealed class BulkRequestFactory : IBulkRequestFactory
{
    public Request? BuildUserByScreenName(IReadOnlyDictionary<string, ApiConfig> api) =>
        RequestMerge.Build(api, "UserByScreenName");

    public Request? BuildUserMedia(IReadOnlyDictionary<string, ApiConfig> api) =>
        RequestMerge.Build(api, "UserMedia");
}
