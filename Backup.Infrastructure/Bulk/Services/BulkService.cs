using Backup.Infrastructure.Bulk.Abstractions.Data;
using Backup.Infrastructure.Bulk.Abstractions.Services;
using Backup.Infrastructure.Models.Config.Api;
using Microsoft.Extensions.Logging;

namespace Backup.Infrastructure.Bulk.Services;

public class BulkService(
    ILogger<BulkService> _logger,
    IBulkData _bulkData,
    IBulkImportRunner importRunner,
    IBulkVerifyRunner verifyRunner,
    IBulkPhase1Runner phase1Runner,
    IBulkPhase2Runner phase2Runner,
    IBulkPhase2ResetRunner phase2ResetRunner
) : IBulkService
{
    private readonly ILogger<BulkService> _logger = _logger;
    private readonly IBulkData _bulkData = _bulkData;
    private readonly IBulkImportRunner _importRunner = importRunner;
    private readonly IBulkVerifyRunner _verifyRunner = verifyRunner;
    private readonly IBulkPhase1Runner _phase1Runner = phase1Runner;
    private readonly IBulkPhase2Runner _phase2Runner = phase2Runner;
    private readonly IBulkPhase2ResetRunner _phase2ResetRunner = phase2ResetRunner;

    public async Task Download(UsersContext context)
    {
        using CancellationTokenSource tokenSource = new();
        IReadOnlyDictionary<string, ApiConfig> api = context.Api;

        await _importRunner.Run(api, tokenSource.Token);
        await _verifyRunner.Run();
        await _phase1Runner.Run(api, tokenSource.Token);
        await _phase2Runner.Run(api, tokenSource.Token);
        await _phase2ResetRunner.Run();

        await _bulkData.Prune();
    }
}
