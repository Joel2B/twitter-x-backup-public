using Backup.Infrastructure.Interfaces.Data.Bulk;
using Backup.Infrastructure.Interfaces.Data.Posts;
using Backup.Infrastructure.Interfaces.Services.Bulk;
using Backup.Infrastructure.Interfaces.Services.Media;
using Backup.Infrastructure.Models.Bulk;
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
    IBulkSourceRouteProvider bulkSourceRouteProvider,
    IBulkApiClient bulkApiClient
) : IBulkService
{
    private readonly ILogger<BulkService> _logger = _logger;

    private readonly AppConfig _config = _config;
    private readonly IPostDomainData _postData = _postData;
    private readonly IBulkSourceData _bulkSourceData = _bulkSourceData;
    private readonly IBulkData _bulkData = _bulkData;
    private readonly IBulkSourceRouteProvider _bulkSourceRouteProvider = bulkSourceRouteProvider;
    private readonly IBulkApiClient _bulkApiClient = bulkApiClient;

    private readonly CancellationTokenSource _tokenSource = new();
    private UsersContext? _context;

    private Dictionary<string, ApiConfig> Api =>
        _context?.Api ?? throw new Exception("bulk context not initialized");

    public async Task Download(UsersContext context)
    {
        _context = context;

        await Import();
        await Verify();
        await Phase1();
        await Phase2();
        await ResetPhase2();

        await _bulkData.Prune();
    }
}


