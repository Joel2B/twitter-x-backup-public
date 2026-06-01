using Backup.Application.Bulk;
using Backup.Application.Bulk.Models;
using Backup.Infrastructure.Bulk.Abstractions.Data;
using Backup.Infrastructure.Bulk.Abstractions.Services;
using Backup.Infrastructure.Bulk.Adapters;
using Backup.Infrastructure.Posts.Abstractions.Data;
using Microsoft.Extensions.Logging;

namespace Backup.Infrastructure.Bulk.Services;

public sealed class BulkVerifyRunner(
    ILogger<BulkVerifyRunner> logger,
    IPostDomainData postData,
    IBulkData bulkData,
    IBulkVerifyService bulkVerifyService
) : IBulkVerifyRunner
{
    private readonly ILogger<BulkVerifyRunner> _logger = logger;
    private readonly IPostDomainData _postData = postData;
    private readonly IBulkData _bulkData = bulkData;
    private readonly IBulkVerifyService _bulkVerifyService = bulkVerifyService;

    public async Task Run(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _logger.LogInformation("running verify");

        IReadOnlyList<BulkVerifyRow> rows = await _bulkVerifyService.Run(
            new BulkVerifyCommandAdapter(_postData, _bulkData),
            cancellationToken
        );

        foreach (BulkVerifyRow item in rows)
        {
            _logger.LogInformation(
                "{userId,-19} {userName,-20} {totalBulk,-4} {totalPost,-4}",
                item.UserId,
                item.UserName,
                item.TotalBulk,
                item.TotalPost
            );
        }
    }
}
