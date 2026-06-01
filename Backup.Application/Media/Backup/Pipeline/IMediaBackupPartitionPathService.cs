using Backup.Application.Media.Backup.Models;

namespace Backup.Application.Media.Backup;

public interface IMediaBackupPartitionPathService
{
    string GetRequiredBackupRootPath(IEnumerable<MediaBackupPartitionPathCandidate> partitions);
}
