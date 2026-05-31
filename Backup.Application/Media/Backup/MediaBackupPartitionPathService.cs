using Backup.Application.Media.Backup.Models;

namespace Backup.Application.Media.Backup;

public sealed class MediaBackupPartitionPathService : IMediaBackupPartitionPathService
{
    public string GetRequiredBackupRootPath(IEnumerable<MediaBackupPartitionPathCandidate> partitions)
    {
        MediaBackupPartitionPathCandidate? candidate = partitions.FirstOrDefault(partition =>
            string.Equals(partition.Type, "backup", StringComparison.OrdinalIgnoreCase)
        );

        if (candidate is null)
            throw new InvalidOperationException("Backup partition not configured.");

        return candidate.RootPath;
    }
}
