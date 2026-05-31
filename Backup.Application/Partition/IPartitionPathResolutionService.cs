using Backup.Application.Partition.Models;

namespace Backup.Application.Partition;

public interface IPartitionPathResolutionService
{
    string Resolve(PartitionPathSource source);
}
