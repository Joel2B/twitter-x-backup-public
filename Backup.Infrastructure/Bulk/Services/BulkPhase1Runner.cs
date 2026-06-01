using Backup.Application.Bulk;
using Backup.Application.Bulk.Models;
using Backup.Infrastructure.Bulk.Abstractions.Data;
using Backup.Infrastructure.Bulk.Abstractions.Services;
using Backup.Infrastructure.Bulk.Adapters;
using Backup.Infrastructure.Models.Config;
using Backup.Infrastructure.Models.Config.Api;
using Backup.Infrastructure.Posts.Abstractions.Data;
using Microsoft.Extensions.Logging;

namespace Backup.Infrastructure.Bulk.Services;

public sealed class BulkPhase1Runner(
    ILogger<BulkPhase1Runner> logger,
    AppConfig config,
    IBulkItemIdentityService bulkItemIdentityService,
    IBulkIdentityLastWriteWinsService bulkIdentityLastWriteWinsService,
    IPostDomainData postData,
    IBulkData bulkData,
    IBulkSourceRouteService bulkSourceRouteService,
    IBulkApiClient bulkApiClient,
    IBulkPhase1Service bulkPhase1Service
) : IBulkPhase1Runner
{
    private readonly ILogger<BulkPhase1Runner> _logger = logger;
    private readonly AppConfig _config = config;
    private readonly IBulkItemIdentityService _bulkItemIdentityService = bulkItemIdentityService;
    private readonly IBulkIdentityLastWriteWinsService _bulkIdentityLastWriteWinsService =
        bulkIdentityLastWriteWinsService;
    private readonly IPostDomainData _postData = postData;
    private readonly IBulkData _bulkData = bulkData;
    private readonly IBulkSourceRouteService _bulkSourceRouteService = bulkSourceRouteService;
    private readonly IBulkApiClient _bulkApiClient = bulkApiClient;
    private readonly IBulkPhase1Service _bulkPhase1Service = bulkPhase1Service;

    public async Task Run(
        IReadOnlyDictionary<string, ApiConfig> api,
        CancellationToken cancellationToken
    )
    {
        _logger.LogInformation("running phase 1");

        if (
            !BulkRunnerExecution.TryResolveOrigin(
                _logger,
                _bulkSourceRouteService,
                out string? origin
            )
        )
            return;

        BulkPhase1Options options = BulkRunnerExecution.CreatePhase1Options(_config);

        await _bulkPhase1Service.Run(
            new BulkPhase1CommandAdapter(
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
