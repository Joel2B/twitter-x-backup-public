using Backup.Infrastructure.Interfaces.Data.Bulk;
using Backup.Infrastructure.Interfaces.Data.Posts;
using Backup.Infrastructure.Interfaces.Services.Bulk;
using Backup.Infrastructure.Interfaces.Services.Media;
using Backup.Infrastructure.Models.Config;
using Backup.Infrastructure.Models.Config.Api;
using Microsoft.Extensions.Logging;

namespace Backup.Infrastructure.Services.Bulk;

public partial class BulkService(
    ILogger<BulkService> _logger,
    AppConfig _config,
    IPostDomainData _postData,
    IBulkSourceData _bulkSourceData,
    IBulkData _bulkData,
    IBulkApiClient bulkApiClient,
    IBulkPhase1Runner phase1Runner,
    IBulkPhase2Runner phase2Runner
) : IBulkService
{
    private readonly ILogger<BulkService> _logger = _logger;

    private readonly AppConfig _config = _config;
    private readonly IPostDomainData _postData = _postData;
    private readonly IBulkSourceData _bulkSourceData = _bulkSourceData;
    private readonly IBulkData _bulkData = _bulkData;
    private readonly IBulkApiClient _bulkApiClient = bulkApiClient;
    private readonly IBulkPhase1Runner _phase1Runner = phase1Runner;
    private readonly IBulkPhase2Runner _phase2Runner = phase2Runner;

    public async Task Download(UsersContext context)
    {
        using CancellationTokenSource tokenSource = new();
        IReadOnlyDictionary<string, ApiConfig> api = context.Api;

        await Import(api, tokenSource.Token);
        await Verify();
        await _phase1Runner.Run(api, tokenSource.Token);
        await _phase2Runner.Run(api, tokenSource.Token);
        await ResetPhase2();

        await _bulkData.Prune();
    }
}


