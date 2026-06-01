using Backup.Application.Bulk.Models;
using Backup.Application.Bulk.Ports;

namespace Backup.Application.Bulk;

public sealed class BulkPhase2ResetService : IBulkPhase2ResetService
{
    public async Task Run(IBulkPhase2ResetCommand command)
    {
        IReadOnlyList<BulkItem> data = await command.GetBulks();

        if (
            data.Where(item => item.UserStatus == BulkUserStatus.Active)
                .Any(item => item.Phase2Order is not null)
        )
            return;

        foreach (BulkItem bulk in data)
            bulk.Phase2Order = 0;

        await command.SaveBulks(data);
    }
}
