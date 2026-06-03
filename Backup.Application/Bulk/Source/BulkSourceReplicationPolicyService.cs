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

    public IReadOnlyList<string> GetMissingFilesFromPaths(
        IEnumerable<string> primaryFilePaths,
        IEnumerable<string> replicaFilePaths
    )
    {
        IReadOnlyList<string> primaryFileNames = primaryFilePaths
            .Select(Path.GetFileName)
            .Where(fileName => !string.IsNullOrWhiteSpace(fileName))
            .Select(fileName => fileName!)
            .ToList();
        IReadOnlyList<string> replicaFileNames = replicaFilePaths
            .Select(Path.GetFileName)
            .Where(fileName => !string.IsNullOrWhiteSpace(fileName))
            .Select(fileName => fileName!)
            .ToList();

        return GetMissingFiles(primaryFileNames, replicaFileNames);
    }
}
