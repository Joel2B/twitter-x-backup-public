using Backup.Application.IO;
using Backup.Infrastructure.Core.Abstractions.Partition;
using Backup.Infrastructure.Models.Config.Data;
using Backup.Infrastructure.Models.Config.Data.Dump;

namespace Backup.Infrastructure.Dump.Data;

internal sealed class LocalDumpsDataPathLayout(
    StorageDump config,
    IPartition partition,
    IDataStoreGuardService dataStoreGuardService
)
{
    private readonly StorageDump _config = config;
    private readonly IPartition _partition = partition;
    private readonly IDataStoreGuardService _dataStoreGuardService = dataStoreGuardService;

    public void EnsureDirectories()
    {
        foreach (PartitionConfig partition in _partition.GetPartitions())
            Directory.CreateDirectory(GetDirectoryPath(partition));
    }

    public string GetFilePath(PartitionConfig? partition = null)
    {
        string fileName = _dataStoreGuardService.RequireConfiguredFileName(
            _config.Paths.Dumps.File
        );
        PartitionConfig primary = partition ?? _partition.GetPrimary();

        return Path.Combine(GetDirectoryPath(primary), fileName);
    }

    private string GetDirectoryPath(PartitionConfig partition) =>
        Path.Combine([.. partition.Paths, .. _config.Paths.Paths, .. _config.Paths.Dumps.Paths]);
}
