namespace Backup.Application.Bulk;

public interface IBulkSourceReplicationPolicyService
{
    IReadOnlyList<string> GetMissingFiles(
        IEnumerable<string> primaryFileNames,
        IEnumerable<string> replicaFileNames
    );
}
