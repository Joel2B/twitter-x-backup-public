using Backup.Application.Bulk.Models;
using Backup.Application.Bulk.Ports;

namespace Backup.Application.Bulk;

public sealed class BulkVerifyService : IBulkVerifyService
{
    public async Task<IReadOnlyList<BulkVerifyRow>> Run(
        IBulkVerifyCommand command,
        CancellationToken cancellationToken = default
    )
    {
        cancellationToken.ThrowIfCancellationRequested();
        IReadOnlyList<BulkItem> bulks = await command.GetBulks(cancellationToken);

        List<BulkItem> filtered = bulks
            .Where(item => item.UserStatus == BulkUserStatus.Active && item.Phase1Order is null)
            .ToList();

        List<string> userIds = filtered
            .Select(item => item.UserId ?? string.Empty)
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Distinct(StringComparer.Ordinal)
            .ToList();

        cancellationToken.ThrowIfCancellationRequested();
        Dictionary<string, int> postCounts = await command.GetPostCountsByProfileIds(
            userIds,
            cancellationToken
        );

        return filtered
            .Select(item => new BulkVerifyRow
            {
                UserId = item.UserId,
                UserName = item.UserName,
                TotalBulk = item.Total,
                TotalPost =
                    !string.IsNullOrWhiteSpace(item.UserId)
                    && postCounts.TryGetValue(item.UserId, out int totalPost)
                        ? totalPost
                        : 0,
            })
            .ToList();
    }
}
