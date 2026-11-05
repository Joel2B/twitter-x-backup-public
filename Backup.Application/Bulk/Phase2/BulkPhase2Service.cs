using Backup.Application.Bulk.Models;
using Backup.Application.Bulk.Ports;
using Backup.Domain.Posts;

namespace Backup.Application.Bulk;

public sealed class BulkPhase2Service : IBulkPhase2Service
{
    public async Task Run(
        IBulkPhase2Command command,
        BulkPhase2Options options,
        string origin,
        CancellationToken cancellationToken
    )
    {
        IReadOnlyList<BulkItem> data = await command.GetBulks();

        List<BulkItem> bulks = data.Where(item =>
                item.UserStatus == BulkUserStatus.Active && item.Phase2Order is not null
            )
            .Take(options.UsersPerPhase2)
            .ToList();

        int progress = 1;

        foreach (BulkItem bulk in bulks)
        {
            string userId = EnsureUserId(bulk);
            Dictionary<MediaTimelineType, ParseResult> firstPages = [];
            bool failed = false;

            foreach (MediaTimelineType timeline in Enum.GetValues<MediaTimelineType>())
            {
                if (IsCompleted(bulk, timeline))
                    continue;

                ParseResult? result = await GetPage(
                    command,
                    options,
                    timeline,
                    bulk,
                    userId,
                    origin,
                    cancellationToken
                );

                if (result is null)
                {
                    failed = true;
                    break;
                }

                firstPages[timeline] = result;
                ApplyPageState(bulk, timeline, result);
            }

            if (failed)
            {
                List<Post> partialPosts = firstPages
                    .Values.SelectMany(result => result.Posts)
                    .GroupBy(post => post.Id, StringComparer.Ordinal)
                    .Select(group => group.Last())
                    .OrderByDescending(post => post.Id, StringComparer.Ordinal)
                    .ToList();
                ApplyIndex(partialPosts, userId, origin);

                if (partialPosts.Count > 0)
                    await command.AddPosts(userId, origin, partialPosts);

                continue;
            }

            int? mediaCount = firstPages
                .Values.SelectMany(result => result.Posts)
                .Select(post => post.Profile.Count?.Media)
                .FirstOrDefault(count => count is not null);

            List<Post> initialPosts = firstPages
                .Values.SelectMany(result => result.Posts)
                .GroupBy(post => post.Id, StringComparer.Ordinal)
                .Select(group => group.Last())
                .OrderByDescending(post => post.Id, StringComparer.Ordinal)
                .ToList();

            if (firstPages.Count == 0)
            {
                CompletePhase(bulk);
                continue;
            }

            if (firstPages.Values.All(result => result.Posts.Count == 0))
            {
                CompletePhase(bulk);
                continue;
            }

            if (mediaCount is null || bulk.Total is null)
            {
                ApplyIndex(initialPosts, userId, origin);

                if (initialPosts.Count > 0)
                    await command.AddPosts(userId, origin, initialPosts);

                continue;
            }

            if (mediaCount <= bulk.Total)
            {
                CompletePhase(bulk);
                continue;
            }

            int diff = mediaCount.Value - bulk.Total.Value;
            List<Post> posts = [];
            bool covered = true;

            foreach (MediaTimelineType timeline in firstPages.Keys)
            {
                ParseResult result = firstPages[timeline];
                HashSet<string> timelineIds = new(StringComparer.Ordinal);

                while (true)
                {
                    posts.AddRange(result.Posts);

                    foreach (Post post in result.Posts)
                        timelineIds.Add(post.Id);

                    if (timelineIds.Count >= diff || IsCompleted(bulk, timeline))
                        break;

                    if (timelineIds.Count >= options.MaxCountPostPhase2)
                    {
                        covered = false;
                        break;
                    }

                    ParseResult? next = await GetPage(
                        command,
                        options,
                        timeline,
                        bulk,
                        userId,
                        origin,
                        cancellationToken
                    );

                    if (next is null)
                    {
                        covered = false;
                        break;
                    }

                    result = next;
                    ApplyPageState(bulk, timeline, result);
                }
            }

            List<Post> uniquePosts = posts
                .GroupBy(post => post.Id, StringComparer.Ordinal)
                .Select(group => group.Last())
                .OrderByDescending(post => post.Id, StringComparer.Ordinal)
                .ToList();
            ApplyIndex(uniquePosts, userId, origin);

            if (uniquePosts.Count > 0)
                await command.AddPosts(userId, origin, uniquePosts);

            if (covered)
            {
                bulk.Total = mediaCount;
                CompletePhase(bulk);
            }
            else
            {
                bulk.Phase2Order = (bulk.Phase2Order ?? 0) + 1;
            }

            if (progress % options.SavePerAction == 0)
            {
                await command.SavePosts();
                await command.SaveBulks(data);
            }

            progress++;
        }

        await command.SavePosts();
        await command.SaveBulks(data);
    }

    private static string EnsureUserId(BulkItem bulk)
    {
        if (string.IsNullOrWhiteSpace(bulk.UserId))
            throw new InvalidOperationException("bulk user id is required");

        return bulk.UserId;
    }

    private static async Task<ParseResult?> GetPage(
        IBulkPhase2Command command,
        BulkPhase2Options options,
        MediaTimelineType timeline,
        BulkItem bulk,
        string userId,
        string origin,
        CancellationToken cancellationToken
    )
    {
        if (!await command.VerifyApi())
            return null;

        for (int attempt = 0; attempt < options.ApiRetryCount; attempt++)
        {
            ParseResult? result = await command.GetUserMedia(
                timeline,
                userId,
                bulk.UserName,
                origin,
                options.MediaPerApi,
                GetCursor(bulk, timeline),
                cancellationToken
            );

            if (result is not null)
                return result;
        }

        return null;
    }

    private static string? GetCursor(BulkItem bulk, MediaTimelineType timeline) =>
        timeline == MediaTimelineType.Video ? bulk.Phase2VideoCursor : bulk.Phase2PhotoCursor;

    private static bool IsCompleted(BulkItem bulk, MediaTimelineType timeline) =>
        timeline == MediaTimelineType.Video ? bulk.Phase2VideoCompleted : bulk.Phase2PhotoCompleted;

    private static void ApplyPageState(
        BulkItem bulk,
        MediaTimelineType timeline,
        ParseResult result
    )
    {
        bool completed = result.Posts.Count == 0 || result.NextCursor is null;

        if (timeline == MediaTimelineType.Video)
        {
            bulk.Phase2VideoCursor = result.NextCursor;
            bulk.Phase2VideoCompleted = completed;
        }
        else
        {
            bulk.Phase2PhotoCursor = result.NextCursor;
            bulk.Phase2PhotoCompleted = completed;
        }
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

    private static void CompletePhase(BulkItem bulk)
    {
        bulk.Phase2Order = null;
        ResetTimelines(bulk);
    }

    private static void ResetTimelines(BulkItem bulk)
    {
        bulk.Cursor = null;
        bulk.Phase2VideoCursor = null;
        bulk.Phase2VideoCompleted = false;
        bulk.Phase2PhotoCursor = null;
        bulk.Phase2PhotoCompleted = false;
    }
}
