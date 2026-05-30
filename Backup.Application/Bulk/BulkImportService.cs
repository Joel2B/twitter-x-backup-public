using Backup.Application.Bulk.Models;
using Backup.Application.Bulk.Ports;
using Backup.Domain.Posts;

namespace Backup.Application.Bulk;

public sealed class BulkImportService : IBulkImportService
{
    public async Task Run(
        IBulkImportCommand command,
        BulkImportOptions options,
        CancellationToken cancellationToken
    )
    {
        IReadOnlyList<BulkSourceItem> sources = await command.GetSources();
        IReadOnlyList<BulkItem> bulkSnapshot = await command.GetBulks();
        List<BulkItem> bulks = bulkSnapshot.ToList();

        List<BulkSourceItem> filtered = sources
            .Where(source =>
                source.Type == BulkSourceType.Media
                && !bulks.Any(item =>
                    string.Equals(item.UserName, source.UserName, StringComparison.Ordinal)
                )
            )
            .ToList();

        if (options.UsersPerCycle > 0)
            filtered = filtered.Take(options.UsersPerCycle).ToList();

        foreach (BulkSourceItem source in filtered)
        {
            BulkItem bulk = new()
            {
                UserName = source.UserName,
                UserStatus = BulkUserStatus.None,
            };

            bool valid = await command.VerifyApi();

            if (!valid)
                break;

            ParseUser? result = await command.GetUserByUser(source.UserName, cancellationToken);

            if (result is null)
                continue;

            if (result.User is not null)
            {
                bulk.UserId = result.User.Id;
                bulk.UserStatus = BulkUserStatus.Active;
                bulk.Total = result.User.MediaCount;
            }

            bulks.Add(bulk);
        }

        await command.SaveBulks(bulks);
    }
}
