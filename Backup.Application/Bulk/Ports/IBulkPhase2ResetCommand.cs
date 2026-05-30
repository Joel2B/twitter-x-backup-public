using Backup.Application.Bulk.Models;

namespace Backup.Application.Bulk.Ports;

public interface IBulkPhase2ResetCommand
{
    Task<IReadOnlyList<BulkItem>> GetBulks();
    Task SaveBulks(IReadOnlyList<BulkItem> bulks);
}
