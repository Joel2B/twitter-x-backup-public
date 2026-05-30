using Backup.Infrastructure.Models.Config.Data;

namespace Backup.Infrastructure.Interfaces.Partition;

public interface IPartition
{
    public List<PartitionConfig> GetPartitions(List<int>? ids = null);
    public PartitionConfig GetPath(int? id = null, long size = 0);
    public List<PartitionConfig> GetCache();
    public PartitionConfig GetPrimary();
    public PartitionConfig GetHeavy();
    public void SetupSizes(Dictionary<int, long> sizes);
}
