using Backup.App.Interfaces.Data.Post;
using Backup.App.Models.Bulk;
using Microsoft.Extensions.Logging;

namespace Backup.App.Services.Bulk;

public partial class BulkService
{
    private async Task Verify()
    {
        _logger.LogInformation("running verify");
        _logger.LogInformation("getting posts");
        IPostData postData = _postData.First();
        Dictionary<string, Models.Post.Post> posts = await postData.GetAllAsDictionary() ?? [];

        _logger.LogInformation("getting bulks");
        List<Models.Bulk.Bulk>? bulks = await _bulkData.GetBulks();

        if (bulks is null)
        {
            _logger.LogInformation("bulk data is null");
            return;
        }

        List<Models.Bulk.Bulk> bulksFiltered = bulks
            .Where(o => o.User.Status == StatusUser.Active && o.Order.Phase1 is null)
            .ToList();

        var query =
            from u in bulksFiltered
            join p in posts on u.User.Id equals p.Value.Profile.Id into gp
            select new
            {
                UserId = u.User.Id,
                UserName = u.User.Name,
                TotalBulk = u.Total,
                TotalPost = gp.Count(),
            };

        var data = query.ToList();

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
