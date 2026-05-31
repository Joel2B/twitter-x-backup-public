namespace Backup.Application.Bulk;

public sealed class BulkReplicationPathPlanningService : IBulkReplicationPathPlanningService
{
    public IReadOnlyList<string> GetReplicaPaths(
        string primaryFilePath,
        IEnumerable<string> replicaFilePaths
    ) =>
        replicaFilePaths
            .Where(path => !string.Equals(path, primaryFilePath, StringComparison.OrdinalIgnoreCase))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
}
