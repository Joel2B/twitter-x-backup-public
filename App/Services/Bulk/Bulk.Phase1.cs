using Backup.App.Interfaces.Data.Post;
using Backup.App.Models.Bulk;
using Backup.App.Models.Post;
using Microsoft.Extensions.Logging;

namespace Backup.App.Services.Bulk;

public partial class BulkService
{
    private async Task Phase1()
    {
        _logger.LogInformation("running phase 1");

        IPostData postData = _postData.First();
        Dictionary<string, Models.Post.Post> posts = await postData.GetAllAsDictionary() ?? [];
        _logger.LogInformation("post count: {count}", posts.Count);

        List<Models.Bulk.Bulk>? bulks = await _bulkData.GetBulks();

        if (bulks is null)
        {
            _logger.LogInformation("bulk data is null");
            return;
        }

        IQueryable<Models.Bulk.Bulk> query = bulks
            .Where(o => o.User.Status == StatusUser.Active && o.Order.Phase1 is not null)
            .OrderBy(o => o.Order.Phase1)
            .AsQueryable();

        List<Models.Bulk.Bulk> bulksLeft = query.ToList();

        _logger.LogInformation("bulks left: {count}", bulksLeft.Count);

        if (bulksLeft.Count > 0)
            _logger.LogInformation(
                "{names}",
                string.Join(", ", bulksLeft.Select(o => o.User.Name))
            );

        if (_config.Bulk.UsersPerCycle > 0)
            query = query.Take(_config.Bulk.UsersPerCycle);

        List<Models.Bulk.Bulk> bulksFiltered = query.ToList();

        string? origin = GetType(SourceType.Media);

        if (origin is null)
        {
            _logger.LogInformation("origin is null");
            return;
        }

        int progress = 1;

        foreach (Models.Bulk.Bulk bulk in bulksFiltered)
        {
            _logger.LogInformation(
                "progress: {progress}/{total}",
                progress,
                _config.Bulk.UsersPerCycle
            );

            _logger.LogInformation(
                "user id: {id} user name: {name}, media count: {count}",
                bulk.User.Id,
                bulk.User.Name,
                bulk.Total
            );

            int index = 0;
            int count = 0;

            while (_config.Bulk.ApiPerCycle <= 0 || index < _config.Bulk.ApiPerCycle)
            {
                _logger.LogInformation("index: {index}, count: {count}", index, count);

                if (bulk.User.Id is null)
                {
                    _logger.LogInformation("id is null for the user {user}", bulk.User.Name);
                    continue;
                }

                bool valid = await _downloader.Verify();

                if (!valid)
                {
                    _logger.LogInformation("downloader is not valid");
                    break;
                }

                ParseResult? result = null;
                int attempt = 0;

                while (attempt < _config.Bulk.ApiRetryCount)
                {
                    result = await GetUserMedia(
                        bulk.User.Id,
                        origin,
                        _config.Bulk.MediaPerApi,
                        bulk.Cursor
                    );

                    if (result is not null)
                        break;

                    attempt++;

                    _logger.LogWarning("attempt: {attempt}", attempt);
                }

                if (result is null)
                {
                    bulk.User.Status = StatusUser.Inactive;
                    _logger.LogInformation("error in GetUserMedia in the id {id}", bulk.User.Id);
                    break;
                }

                _logger.LogInformation("ParseResult return {count} posts", result.Posts.Count);

                _merger.Merge(bulk.User.Id, origin, posts, result.Posts);

                if (result.Posts.Count == 0 || result.NextCursor is null)
                {
                    bulk.Order.Phase1 = null;
                    bulk.Cursor = null;
                }
                else
                    bulk.Order.Phase1++;

                bulk.Cursor = result.NextCursor;

                index++;
                count += result.Posts.Count;

                _logger.LogInformation("cursor: {cursor}", bulk.Cursor);

                if (bulk.Order.Phase1 is null || count >= _config.Bulk.MaxCountPost)
                    break;
            }

            if (progress % _config.Bulk.SavePerAction == 0)
            {
                _logger.LogInformation("saving posts");
                await postData.Save([.. posts.Values]);

                _logger.LogInformation("saving bulks");
                await _bulkData.Save(bulks);
            }

            progress++;
        }

        _logger.LogInformation("saving posts");
        await postData.Save([.. posts.Values]);

        _logger.LogInformation("saving bulks");
        await _bulkData.Save(bulks);

        _logger.LogInformation("replicating posts");
        await _postReplication.Replicate(_postData);
    }
}
