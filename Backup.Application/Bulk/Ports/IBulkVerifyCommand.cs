using Backup.Application.Bulk.Models;

namespace Backup.Application.Bulk.Ports;

public interface IBulkVerifyCommand
{
    Task<IReadOnlyList<BulkItem>> GetBulks(CancellationToken cancellationToken = default);
    Task<Dictionary<string, int>> GetPostCountsByProfileIds(
        IReadOnlyCollection<string> profileIds,
        CancellationToken cancellationToken = default
    );
}
