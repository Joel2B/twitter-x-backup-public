using Backup.Application.Bulk;
using Backup.Application.Bulk.Models;
using Backup.Infrastructure.Bulk.Abstractions.Data;
using Backup.Infrastructure.Posts.Abstractions.Data;
using Backup.Infrastructure.Bulk.Abstractions.Services;
using Backup.Infrastructure.Models.Config;
using Backup.Infrastructure.Models.Config.Api;
using Backup.Infrastructure.Bulk.Adapters;
using Microsoft.Extensions.Logging;

namespace Backup.Infrastructure.Bulk.Services;

public sealed class BulkPhase2Runner(
    ILogger<BulkPhase2Runner> logger,
    AppConfig config,
    IBulkItemIdentityService bulkItemIdentityService,
    IBulkIdentityLastWriteWinsService bulkIdentityLastWriteWinsService,
    IPostDomainData postData,
    IBulkData bulkData,
    IBulkSourceRouteService bulkSourceRouteService,
    IBulkApiClient bulkApiClient,
    IBulkPhase2Service bulkPhase2Service
) : IBulkPhase2Runner
{
    private readonly ILogger<BulkPhase2Runner> _logger = logger;
    private readonly AppConfig _config = config;
    private readonly IBulkItemIdentityService _bulkItemIdentityService = bulkItemIdentityService;
    private readonly IBulkIdentityLastWriteWinsService _bulkIdentityLastWriteWinsService =
        bulkIdentityLastWriteWinsService;
    private readonly IPostDomainData _postData = postData;
    private readonly IBulkData _bulkData = bulkData;
    private readonly IBulkSourceRouteService _bulkSourceRouteService = bulkSourceRouteService;
    private readonly IBulkApiClient _bulkApiClient = bulkApiClient;
    private readonly IBulkPhase2Service _bulkPhase2Service = bulkPhase2Service;

    public async Task Run(IReadOnlyDictionary<string, ApiConfig> api, CancellationToken cancellationToken)
    {
        _logger.LogInformation("running phase 2");

        if (!BulkRunnerExecution.TryResolveOrigin(_logger, _bulkSourceRouteService, out string? origin))
            return;

        BulkPhase2Options options = BulkRunnerExecution.CreatePhase2Options(_config);

        await _bulkPhase2Service.Run(
            new BulkPhase2CommandAdapter(
                api,
                _bulkItemIdentityService,
                _bulkIdentityLastWriteWinsService,
                _postData,
                _bulkData,
                _bulkApiClient
            ),
            options,
            origin,
            cancellationToken
        );
    }
}
