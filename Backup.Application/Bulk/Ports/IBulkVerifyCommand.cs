using Backup.Application.Bulk.Models;

namespace Backup.Application.Bulk.Ports;

public interface IBulkVerifyCommand
{
    Task<IReadOnlyList<BulkItem>> GetBulks();
    Task<Dictionary<string, int>> GetPostCountsByProfileIds(IReadOnlyCollection<string> profileIds);
}
