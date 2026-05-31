using Backup.Application.Dump.Models;

namespace Backup.Application.Dump;

public sealed class DumpReplicationPlanningService : IDumpReplicationPlanningService
{
    public DumpReplicationPlan Plan(
        string primaryRootPath,
        string currentPath,
        IEnumerable<string> replicaRootPaths
    )
    {
        string relativePath = Path.GetRelativePath(primaryRootPath, currentPath);
        IReadOnlyList<string> targetPaths = replicaRootPaths
            .Select(root => Path.Combine(root, relativePath))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        return new DumpReplicationPlan { RelativePath = relativePath, TargetPaths = targetPaths };
    }
}
