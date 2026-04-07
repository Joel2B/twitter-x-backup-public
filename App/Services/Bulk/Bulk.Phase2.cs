using Backup.App.Interfaces.Data.Post;
using Backup.App.Models.Bulk;
using Backup.App.Models.Post;
using Microsoft.Extensions.Logging;

namespace Backup.App.Services.Bulk;

public partial class BulkService
{
    private async Task Phase2()
    {
        _logger.LogInformation("running phase 2");
        List<Models.Bulk.Bulk>? data = await _bulkData.GetBulks();

        if (data is null)
            return;

        List<Models.Bulk.Bulk> bulks = data.Where(o =>
                o.User.Status == StatusUser.Active && o.Order.Phase2 is not null
            )
            .Take(_config.Bulk.UsersPerPhase2)
            .ToList();

        string? origin = GetType(SourceType.Media);

        if (origin is null)
        {
            _logger.LogInformation("origin is null");
            return;
        }

        IPostData postData = _postData.First();
        Dictionary<string, Models.Post.Post> posts = await postData.GetAllAsDictionary() ?? [];

        int progress = 1;

        foreach (Models.Bulk.Bulk bulk in bulks)
        {
            _logger.LogInformation(
                "progress: {progress}/{total}",
                progress,
                _config.Bulk.UsersPerPhase2
            );

            _logger.LogInformation(
                "user id: {id} user name: {name}, media count: {count}",
                bulk.User.Id,
                bulk.User.Name,
                bulk.Total
            );

            if (bulk.Order.Phase2 == 0)
                bulk.Cursor = null;

            bool valid = await _downloader.Verify();

            if (!valid)
            {
                _logger.LogInformation("downloader is not valid");
                break;
            }

            ParseResult? result = await GetUserMedia(
                bulk.User.Id ?? throw new Exception(),
                origin,
                _config.Bulk.MediaPerApi,
                bulk.Cursor
            );

            if (result is null)
            {
                bulk.User.Status = StatusUser.Inactive;
                _logger.LogInformation("error in GetUserMedia in the id {id}", bulk.User.Id);
                continue;
            }

            if (result.Posts.Count == 0)
            {
                bulk.Order.Phase2 = null;
                continue;
            }

            int? mediaCount = result.Posts[0].Profile.Count?.Media;

            if (mediaCount is null || bulk.Total is null)
                throw new Exception();

            if (mediaCount <= bulk.Total)
            {
                bulk.Order.Phase2 = null;
                continue;
            }

            int diff = (int)mediaCount - (int)bulk.Total;
            int index = 0;
            int count = 0;

            while (true)
            {
                _logger.LogInformation("index: {index}, count: {count}", index, count);

                valid = await _downloader.Verify();

                if (!valid)
                {
                    _logger.LogInformation("downloader is not valid");
                    break;
                }

                int attempt = 0;

                while (result is null && attempt < _config.Bulk.ApiRetryCount)
                {
                    result = await GetUserMedia(
                        bulk.User.Id ?? throw new Exception(),
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
                posts = await postData.AddPosts(bulk.User.Id, origin, result.Posts);

                index++;
                count += result.Posts.Count;

                if (
                    count >= diff
                    || result.Posts.Count == 0
                    || result.NextCursor is null
                    || count >= _config.Bulk.MaxCountPostPhase2
                )
                    break;

                bulk.Cursor = result.NextCursor;
                result = null;
            }

            bulk.Order.Phase2 = null;
            bulk.Total = mediaCount;

            if (progress % _config.Bulk.SavePerAction == 0)
            {
                await postData.Save([.. posts.Values]);
                await _bulkData.Save(data);
            }

            progress++;
        }

        await postData.Save([.. posts.Values]);
        await _bulkData.Save(data);

        _logger.LogInformation("replicating posts");
        await _postReplication.Replicate(_postData);
    }

    private async Task ResetPhase2()
    {
        _logger.LogInformation("reset phase 2");
        List<Models.Bulk.Bulk>? data = await _bulkData.GetBulks();

        if (
            data is null
            || data.Where(o => o.User.Status == StatusUser.Active)
                .Any(o => o.Order.Phase2 is not null)
        )
            return;

        _logger.LogInformation("setting Phase2 = 0");

        foreach (Models.Bulk.Bulk bulk in data)
            bulk.Order.Phase2 = 0;

        await _bulkData.Save(data);
    }
}
