using Backup.Application.Bulk.Models;
using Backup.Application.Bulk.Ports;
using Backup.Domain.Posts;
using Microsoft.Extensions.Logging;

namespace Backup.Application.Bulk;

public sealed class BulkPhase1Service(ILogger<BulkPhase1Service> logger) : IBulkPhase1Service
{
    private readonly ILogger<BulkPhase1Service> _logger = logger;

    public async Task Run(
        IBulkPhase1Command command,
        BulkPhase1Options options,
        string origin,
        CancellationToken cancellationToken
    )
    {
        _logger.LogInformation("bulk phase 1: requesting post count");
        int postCount = await command.GetPostCount();
        _logger.LogInformation("bulk phase 1: current post count is {postCount}", postCount);

        IReadOnlyList<BulkItem> bulks = await command.GetBulks();

        List<BulkItem> active = bulks
            .Where(item => item.UserStatus == BulkUserStatus.Active && item.Phase1Order is not null)
            .OrderBy(item => item.Phase1Order)
            .ToList();

        _logger.LogInformation(
            "bulk phase 1: found {activeCount} active bulks from {totalCount} total",
            active.Count,
            bulks.Count
        );

        if (options.UsersPerCycle > 0)
            active = active.Take(options.UsersPerCycle).ToList();

        _logger.LogInformation(
            "bulk phase 1: using {activeCount} active bulks after UsersPerCycle filter",
            active.Count
        );

        int progress = 1;

        foreach (BulkItem bulk in active)
        {
            if (string.IsNullOrWhiteSpace(bulk.UserId))
                continue;

            int index = 0;
            int count = 0;

            while (options.ApiPerCycle <= 0 || index < options.ApiPerCycle)
            {
                bool valid = await command.VerifyApi();

                if (!valid)
                    break;

                List<Post> posts = [];
                bool failed = false;

                foreach (MediaTimelineType timeline in Enum.GetValues<MediaTimelineType>())
                {
                    if (IsCompleted(bulk, timeline))
                        continue;

                    ParseResult? result = null;

                    for (int attempt = 0; attempt < options.ApiRetryCount; attempt++)
                    {
                        result = await command.GetUserMedia(
                            timeline,
                            bulk.UserId,
                            bulk.UserName,
                            origin,
                            options.MediaPerApi,
                            GetCursor(bulk, timeline),
                            cancellationToken
                        );

                        if (result is not null)
                            break;
                    }

                    if (result is null)
                    {
                        failed = true;
                        break;
                    }

                    posts.AddRange(result.Posts);
                    SetCursor(bulk, timeline, result.NextCursor);
                    SetCompleted(
                        bulk,
                        timeline,
                        result.Posts.Count == 0 || result.NextCursor is null
                    );
                }

                List<Post> uniquePosts = posts
                    .GroupBy(post => post.Id, StringComparer.Ordinal)
                    .Select(group => group.Last())
                    .OrderByDescending(post => post.Id, StringComparer.Ordinal)
                    .ToList();
                ApplyIndex(uniquePosts, bulk.UserId, origin);

                if (uniquePosts.Count > 0)
                    await command.AddPosts(bulk.UserId, origin, uniquePosts);

                if (failed)
                    break;

                if (bulk.VideoCompleted && bulk.PhotoCompleted)
                {
                    bulk.Phase1Order = null;
                    ResetTimelines(bulk);
                }
                else
                {
                    bulk.Phase1Order = (bulk.Phase1Order ?? 0) + 1;
                }

                index++;
                count += uniquePosts.Count;

                if (bulk.Phase1Order is null || count >= options.MaxCountPost)
                    break;
            }

            if (progress % options.SavePerAction == 0)
            {
                await command.SavePosts();
                await command.SaveBulks(bulks);
            }

            progress++;
        }

        await command.SavePosts();
        await command.SaveBulks(bulks);
    }

    private static string? GetCursor(BulkItem bulk, MediaTimelineType timeline) =>
        timeline == MediaTimelineType.Video ? bulk.VideoCursor : bulk.PhotoCursor;

    private static bool IsCompleted(BulkItem bulk, MediaTimelineType timeline) =>
        timeline == MediaTimelineType.Video ? bulk.VideoCompleted : bulk.PhotoCompleted;

    private static void SetCursor(BulkItem bulk, MediaTimelineType timeline, string? cursor)
    {
        if (timeline == MediaTimelineType.Video)
            bulk.VideoCursor = cursor;
        else
            bulk.PhotoCursor = cursor;
    }

    private static void SetCompleted(BulkItem bulk, MediaTimelineType timeline, bool completed)
    {
        if (timeline == MediaTimelineType.Video)
            bulk.VideoCompleted = completed;
        else
            bulk.PhotoCompleted = completed;
    }

    private static void ResetTimelines(BulkItem bulk)
    {
        bulk.Cursor = null;
        bulk.VideoCursor = null;
        bulk.VideoCompleted = false;
        bulk.PhotoCursor = null;
        bulk.PhotoCompleted = false;
    }

    private static void ApplyIndex(List<Post> posts, string userId, string origin)
    {
        for (int i = 0; i < posts.Count; i++)
        {
            posts[i].Index[userId] = posts[i].Index.GetValueOrDefault(userId) ?? [];
            posts[i].Index[userId][origin] = new IndexData
            {
                Previous = i == 0 ? null : posts[i - 1].Id,
                Next = i == posts.Count - 1 ? null : posts[i + 1].Id,
            };
        }
    }
}
