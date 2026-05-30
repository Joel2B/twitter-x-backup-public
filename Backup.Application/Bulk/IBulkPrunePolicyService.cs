using Backup.Application.Bulk.Models;

namespace Backup.Application.Bulk;

public interface IBulkPrunePolicyService
{
    IReadOnlyList<string> GetPathsToRemove(IReadOnlyList<DatedPath> datedPaths, int keepDays);
}
