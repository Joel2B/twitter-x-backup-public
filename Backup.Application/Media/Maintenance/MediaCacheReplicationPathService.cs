namespace Backup.Application.Media.Maintenance;

public sealed class MediaCacheReplicationPathService : IMediaCacheReplicationPathService
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
