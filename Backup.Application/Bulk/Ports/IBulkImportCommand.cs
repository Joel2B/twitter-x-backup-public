using Backup.Application.Bulk.Models;
using Backup.Domain.Posts;

namespace Backup.Application.Bulk.Ports;

public interface IBulkImportCommand
{
    Task<IReadOnlyList<BulkSourceItem>> GetSources();
    Task<IReadOnlyList<BulkItem>> GetBulks();
    Task SaveBulks(IReadOnlyList<BulkItem> bulks);
    Task<bool> VerifyApi();
    Task<ParseUser?> GetUserByUser(string userName, CancellationToken cancellationToken);
}
