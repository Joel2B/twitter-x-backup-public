using Backup.App.Models.Config.Data.Backup;
using Backup.App.Models.Config.Data.Bulk;
using Backup.App.Models.Config.Data.Dump;
using Backup.App.Models.Config.Data.Media;
using Backup.App.Models.Config.Data.Posts;

namespace Backup.App.Models.Config.Data;

public class DataConfig
{
    public required Dictionary<string, string> Aliases { get; set; }
    public required List<PartitionConfig> Partitions { get; set; }
    public required List<StoragePost> Post { get; set; }
    public required List<StorageDump> Dump { get; set; }
    public required List<StorageBulk> Bulk { get; set; }
    public required List<StorageMedia> Media { get; set; }
    public required List<StorageBackup> Backup { get; set; }
}
