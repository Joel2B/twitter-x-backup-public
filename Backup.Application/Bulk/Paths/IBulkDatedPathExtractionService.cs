using Backup.Application.Bulk.Models;

namespace Backup.Application.Bulk;

public interface IBulkDatedPathExtractionService
{
    IReadOnlyList<DatedPath> Extract(IEnumerable<string> paths);
}
