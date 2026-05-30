using Backup.Application.Bulk;
using Backup.Infrastructure.Bulk.Abstractions.Data;
using Backup.Infrastructure.Bulk.Abstractions.Services;
using Backup.Infrastructure.Bulk.Adapters;
using Microsoft.Extensions.Logging;

namespace Backup.Infrastructure.Bulk.Services;

public sealed class BulkPhase2ResetRunner(
    ILogger<BulkPhase2ResetRunner> logger,
    IBulkData bulkData,
    IBulkPhase2ResetService bulkPhase2ResetService
) : IBulkPhase2ResetRunner
{
    private readonly ILogger<BulkPhase2ResetRunner> _logger = logger;
    private readonly IBulkData _bulkData = bulkData;
    private readonly IBulkPhase2ResetService _bulkPhase2ResetService = bulkPhase2ResetService;

    public async Task Run()
    {
        _logger.LogInformation("reset phase 2");

        await _bulkPhase2ResetService.Run(new BulkPhase2ResetCommandAdapter(_bulkData));
    }
}
