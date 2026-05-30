using Backup.Application.Bulk.Models;
using Backup.Application.Bulk.Ports;

namespace Backup.Application.Bulk;

public sealed class BulkVerifyService : IBulkVerifyService
{
    public async Task<IReadOnlyList<BulkVerifyRow>> Run(IBulkVerifyCommand command)
    {
        IReadOnlyList<BulkItem> bulks = await command.GetBulks();

        List<BulkItem> filtered = bulks
            .Where(item => item.UserStatus == BulkUserStatus.Active && item.Phase1Order is null)
            .ToList();

        List<string> userIds = filtered
            .Select(item => item.UserId ?? string.Empty)
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Distinct(StringComparer.Ordinal)
            .ToList();

        Dictionary<string, int> postCounts = await command.GetPostCountsByProfileIds(userIds);

        return filtered
            .Select(item => new BulkVerifyRow
            {
                UserId = item.UserId,
                UserName = item.UserName,
                TotalBulk = item.Total,
                TotalPost = !string.IsNullOrWhiteSpace(item.UserId)
                    && postCounts.TryGetValue(item.UserId, out int totalPost)
                    ? totalPost
                    : 0,
            })
            .ToList();
    }
}
