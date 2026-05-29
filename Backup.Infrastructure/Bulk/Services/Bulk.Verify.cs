using Backup.Infrastructure.Interfaces.Data.Posts;
using Backup.Infrastructure.Models.Bulk;
using Microsoft.Extensions.Logging;

namespace Backup.Infrastructure.Services.Bulk;

public partial class BulkService
{
    private async Task Verify()
    {
        _logger.LogInformation("running verify");
        IPostData postData = _postData;

        _logger.LogInformation("getting bulks");
        List<BulkData>? bulks = await _bulkData.GetBulks();

        if (bulks is null)
        {
            _logger.LogInformation("bulk data is null");
            return;
        }

        List<BulkData> bulksFiltered = bulks
            .Where(o => o.User.Status == StatusUser.Active && o.Order.Phase1 is null)
            .ToList();

        List<string> userIds = bulksFiltered
            .Select(o => o.User.Id ?? "")
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Distinct(StringComparer.Ordinal)
            .ToList();

        _logger.LogInformation("getting post counts by profile ids: {count}", userIds.Count);
        Dictionary<string, int> postCounts = await postData.GetPostCountsByProfileIds(userIds);

        var data = bulksFiltered.Select(bulk => new
        {
            UserId = bulk.User.Id,
            UserName = bulk.User.Name,
            TotalBulk = bulk.Total,
            TotalPost = !string.IsNullOrWhiteSpace(bulk.User.Id)
            && postCounts.TryGetValue(bulk.User.Id, out int totalPost)
                ? totalPost
                : 0,
        });

        foreach (var item in data)
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


