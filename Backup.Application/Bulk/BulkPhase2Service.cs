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

        List<BulkItem> bulks = data
            .Where(item => item.UserStatus == BulkUserStatus.Active && item.Phase2Order is not null)
            .Take(options.UsersPerPhase2)
            .ToList();

        int progress = 1;

        foreach (BulkItem bulk in bulks)
        {
            if (bulk.Phase2Order == 0)
                bulk.Cursor = null;

            bool valid = await command.VerifyApi();

            if (!valid)
                break;

            string userId = EnsureUserId(bulk);

            ParseResult? result = await command.GetUserMedia(
                userId,
                origin,
                options.MediaPerApi,
                bulk.Cursor,
                cancellationToken
            );

            if (result is null)
            {
                bulk.UserStatus = BulkUserStatus.Inactive;
                continue;
            }

            if (result.Posts.Count == 0)
            {
                bulk.Phase2Order = null;
                continue;
            }

            int? mediaCount = result.Posts[0].Profile.Count?.Media;

            if (mediaCount is null || bulk.Total is null)
                continue;

            if (mediaCount <= bulk.Total)
            {
                bulk.Phase2Order = null;
                continue;
            }

            int diff = mediaCount.Value - bulk.Total.Value;
            int index = 0;
            int count = 0;

            while (true)
            {
                valid = await command.VerifyApi();

                if (!valid)
                    break;

                int attempt = 0;

                while (result is null && attempt < options.ApiRetryCount)
                {
                    result = await command.GetUserMedia(
                        userId,
                        origin,
                        options.MediaPerApi,
                        bulk.Cursor,
                        cancellationToken
                    );

                    if (result is not null)
                        break;

                    attempt++;
                }

                if (result is null)
                {
                    bulk.UserStatus = BulkUserStatus.Inactive;
                    break;
                }

                await command.AddPosts(userId, origin, result.Posts);

                index++;
                count += result.Posts.Count;

                if (
                    count >= diff
                    || result.Posts.Count == 0
                    || result.NextCursor is null
                    || count >= options.MaxCountPostPhase2
                )
                    break;

                bulk.Cursor = result.NextCursor;
                result = null;
            }

            bulk.Phase2Order = null;
            bulk.Total = mediaCount;

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
}
