using Backup.Infrastructure.Models.Bulk;
using Microsoft.Extensions.Logging;

namespace Backup.Infrastructure.Services.Bulk;

public partial class BulkService
{
    private async Task ResetPhase2()
    {
        _logger.LogInformation("reset phase 2");
        List<BulkData>? data = await _bulkData.GetBulks();

        if (
            data is null
            || data.Where(o => o.User.Status == StatusUser.Active)
                .Any(o => o.Order.Phase2 is not null)
        )
            return;

        _logger.LogInformation("setting Phase2 = 0");

        foreach (BulkData bulk in data)
            bulk.Order.Phase2 = 0;

        await _bulkData.Save(data);
    }
}

