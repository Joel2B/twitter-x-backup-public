using Backup.Application.Bulk.Models;

namespace Backup.Application.Bulk;

public interface IBulkSourceExtractionService
{
    IReadOnlyList<BulkSourceLinkItem> Extract(IEnumerable<string> lines);
}
