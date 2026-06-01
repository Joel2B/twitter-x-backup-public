using Backup.Application.Bulk.Models;

namespace Backup.Application.Bulk.Ports;

public interface IBulkPhase2ResetCommand
{
    Task<IReadOnlyList<BulkItem>> GetBulks(CancellationToken cancellationToken = default);
    Task SaveBulks(IReadOnlyList<BulkItem> bulks, CancellationToken cancellationToken = default);
}
