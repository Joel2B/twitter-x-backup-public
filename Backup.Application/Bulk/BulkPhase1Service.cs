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

                ParseResult? result = null;
                int attempt = 0;

                while (attempt < options.ApiRetryCount)
                {
                    result = await command.GetUserMedia(
                        bulk.UserId,
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

                await command.AddPosts(bulk.UserId, origin, result.Posts);

                if (result.Posts.Count == 0 || result.NextCursor is null)
                {
                    bulk.Phase1Order = null;
                    bulk.Cursor = null;
                }
                else
                {
                    bulk.Phase1Order = (bulk.Phase1Order ?? 0) + 1;
                }

                bulk.Cursor = result.NextCursor;

                index++;
                count += result.Posts.Count;

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
}
