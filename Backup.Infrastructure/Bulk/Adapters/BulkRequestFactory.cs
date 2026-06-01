using Backup.Application.Config;
using Backup.Infrastructure.Adapters;
using Backup.Infrastructure.Bulk.Abstractions.Services;
using Backup.Infrastructure.Models.Config.Api;
using Backup.Infrastructure.Models.Config.Request;

namespace Backup.Infrastructure.Bulk.Adapters;

public sealed class BulkRequestFactory(IApiRequestBuildService apiRequestBuildService)
    : IBulkRequestFactory
{
    private readonly IApiRequestBuildService _apiRequestBuildService = apiRequestBuildService;

    public Request? BuildUserByScreenName(IReadOnlyDictionary<string, ApiConfig> api) =>
        Build(api, "UserByScreenName");

    public Request? BuildUserMedia(IReadOnlyDictionary<string, ApiConfig> api) =>
        Build(api, "UserMedia");

    private Request? Build(IReadOnlyDictionary<string, ApiConfig> api, string key)
    {
        var built = _apiRequestBuildService.Build(ApiRequestBuildMapper.ToSources(api), key);
        return built is null ? null : ApiRequestBuildMapper.ToRequest(built);
    }
}
