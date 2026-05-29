using Backup.Infrastructure.Models.Config.Data.Backup;
using Backup.Infrastructure.Models.Config.Data.Bulk;
using Backup.Infrastructure.Models.Config.Data.Dump;
using Backup.Infrastructure.Models.Config.Data.Media;
using Backup.Infrastructure.Models.Config.Data.Posts;

namespace Backup.Infrastructure.Models.Config.Data;

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

