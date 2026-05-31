namespace Backup.Application.Media.Maintenance;

public interface IMediaCacheReplicationPathService
{
    IReadOnlyList<string> GetReplicaPaths(
        string primaryFilePath,
        IEnumerable<string> replicaFilePaths
    );
}
