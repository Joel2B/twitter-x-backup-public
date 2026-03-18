namespace Backup.App.Interfaces.Partition;

public interface IPartition
{
    public List<Models.Config.Data.Partition> GetPartitions(List<int>? ids = null);
    public Models.Config.Data.Partition GetPath(int? id = null, long size = 0);
    public List<Models.Config.Data.Partition> GetCache();
    public Models.Config.Data.Partition GetPrimary();
    public Models.Config.Data.Partition GetHeavy();
    public void SetupSizes(Dictionary<int, long> sizes);
}
