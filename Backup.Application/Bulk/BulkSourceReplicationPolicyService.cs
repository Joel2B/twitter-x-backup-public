namespace Backup.Application.Bulk;

public sealed class BulkSourceReplicationPolicyService : IBulkSourceReplicationPolicyService
{
    public IReadOnlyList<string> GetMissingFiles(
        IEnumerable<string> primaryFileNames,
        IEnumerable<string> replicaFileNames
    )
    {
        HashSet<string> replica = [.. replicaFileNames];
        return primaryFileNames.Where(fileName => !replica.Contains(fileName)).ToList();
    }
}
