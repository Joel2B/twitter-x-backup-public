using Backup.Application.Bulk;
using Backup.Application.Bulk.Models;
using Backup.Infrastructure.Bulk.Abstractions.Data;
using Backup.Infrastructure.Bulk.Abstractions.Services;
using Backup.Infrastructure.Bulk.Adapters;
using Backup.Infrastructure.Models.Config;
using Backup.Infrastructure.Models.Config.Api;
using Microsoft.Extensions.Logging;

namespace Backup.Infrastructure.Bulk.Services;

public sealed class BulkImportRunner(
    ILogger<BulkImportRunner> logger,
    AppConfig config,
    IBulkSourceData bulkSourceData,
    IBulkData bulkData,
    IBulkApiClient bulkApiClient,
    IBulkImportService bulkImportService
) : IBulkImportRunner
{
    private readonly ILogger<BulkImportRunner> _logger = logger;
    private readonly AppConfig _config = config;
    private readonly IBulkSourceData _bulkSourceData = bulkSourceData;
    private readonly IBulkData _bulkData = bulkData;
    private readonly IBulkApiClient _bulkApiClient = bulkApiClient;
    private readonly IBulkImportService _bulkImportService = bulkImportService;

    public async Task Run(IReadOnlyDictionary<string, ApiConfig> api, CancellationToken cancellationToken)
    {
        _logger.LogInformation("running import");

        BulkImportOptions options = new() { UsersPerCycle = _config.Bulk.UsersPerCycle };

        await _bulkImportService.Run(
            new BulkImportCommandAdapter(api, _bulkSourceData, _bulkData, _bulkApiClient),
            options,
            cancellationToken
        );
    }
}
