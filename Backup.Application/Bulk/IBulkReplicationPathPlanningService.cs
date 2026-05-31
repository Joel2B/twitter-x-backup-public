namespace Backup.Application.Bulk;

public interface IBulkReplicationPathPlanningService
{
    IReadOnlyList<string> GetReplicaPaths(
        string primaryFilePath,
        IEnumerable<string> replicaFilePaths
    );
}
