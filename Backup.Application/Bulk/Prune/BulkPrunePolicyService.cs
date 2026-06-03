using Backup.Application.Bulk.Models;

namespace Backup.Application.Bulk;

public sealed class BulkPrunePolicyService : IBulkPrunePolicyService
{
    public IReadOnlyList<string> GetPathsToRemove(IReadOnlyList<DatedPath> datedPaths, int keepDays)
    {
        if (datedPaths.Count == 0)
            return [];

        int normalizedKeepDays = Math.Max(0, keepDays);
        DateTime latest = datedPaths.Max(path => path.Date);
        DateTime threshold = latest.AddDays(-normalizedKeepDays);

        return [.. datedPaths.Where(path => path.Date < threshold).Select(path => path.Path)];
    }
}
