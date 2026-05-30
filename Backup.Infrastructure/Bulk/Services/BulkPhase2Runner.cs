using Backup.Application.Bulk;
using Backup.Application.Bulk.Models;
using Backup.Infrastructure.Bulk.Abstractions.Data;
using Backup.Infrastructure.Posts.Abstractions.Data;
using Backup.Infrastructure.Bulk.Abstractions.Services;
using Backup.Infrastructure.Bulk.Models;
using Backup.Infrastructure.Models.Config;
using Backup.Infrastructure.Models.Config.Api;
using Backup.Infrastructure.Bulk.Adapters;
using Microsoft.Extensions.Logging;

namespace Backup.Infrastructure.Bulk.Services;

public sealed class BulkPhase2Runner(
    ILogger<BulkPhase2Runner> logger,
    AppConfig config,
    IPostDomainData postData,
    IBulkData bulkData,
    IBulkSourceRouteProvider bulkSourceRouteProvider,
    IBulkApiClient bulkApiClient,
    IBulkPhase2Service bulkPhase2Service
) : IBulkPhase2Runner
{
    private readonly ILogger<BulkPhase2Runner> _logger = logger;
    private readonly AppConfig _config = config;
    private readonly IPostDomainData _postData = postData;
    private readonly IBulkData _bulkData = bulkData;
    private readonly IBulkSourceRouteProvider _bulkSourceRouteProvider = bulkSourceRouteProvider;
    private readonly IBulkApiClient _bulkApiClient = bulkApiClient;
    private readonly IBulkPhase2Service _bulkPhase2Service = bulkPhase2Service;

    public async Task Run(IReadOnlyDictionary<string, ApiConfig> api, CancellationToken cancellationToken)
    {
        _logger.LogInformation("running phase 2");

        string? origin = _bulkSourceRouteProvider.GetOrigin(SourceType.Media);

        if (origin is null)
        {
            _logger.LogInformation("origin is null");
            return;
        }

        BulkPhase2Options options = new()
        {
            UsersPerPhase2 = _config.Bulk.UsersPerPhase2,
            SavePerAction = _config.Bulk.SavePerAction,
            MediaPerApi = _config.Bulk.MediaPerApi,
            MaxCountPostPhase2 = _config.Bulk.MaxCountPostPhase2,
            ApiRetryCount = _config.Bulk.ApiRetryCount,
        };

        await _bulkPhase2Service.Run(
            new BulkPhase2CommandAdapter(api, _postData, _bulkData, _bulkApiClient),
            options,
            origin,
            cancellationToken
        );
    }
}
