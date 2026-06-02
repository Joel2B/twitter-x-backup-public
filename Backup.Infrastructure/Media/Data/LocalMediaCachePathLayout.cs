using Backup.Application.IO;
using Backup.Application.Media.Maintenance;
using Backup.Infrastructure.Core.Abstractions.Partition;
using Backup.Infrastructure.Models.Config.Data;
using Backup.Infrastructure.Models.Config.Data.Media;

namespace Backup.Infrastructure.Media.Data;

internal sealed class LocalMediaCachePathLayout(
    StorageMedia config,
    IPartition partition,
    IDataStoreGuardService dataStoreGuardService,
    IMediaCacheDirectoryPolicyService mediaCacheDirectoryPolicyService
)
{
    private readonly StorageMedia _config = config;
    private readonly IPartition _partition = partition;
    private readonly IDataStoreGuardService _dataStoreGuardService = dataStoreGuardService;
    private readonly IMediaCacheDirectoryPolicyService _mediaCacheDirectoryPolicyService =
        mediaCacheDirectoryPolicyService;

    public void EnsureDirectories()
    {
        foreach (PartitionConfig item in _partition.GetPartitions())
        {
            if (
                _mediaCacheDirectoryPolicyService.ShouldCreateCacheDirectory(item.Type, item.Tags)
            )
                Directory.CreateDirectory(GetCachePath(item));

            if (_mediaCacheDirectoryPolicyService.ShouldCreateMediaDirectory(item.Type))
                Directory.CreateDirectory(GetMediaPath(item));
        }

        Directory.CreateDirectory(GetCacheDownloadPath(_partition.GetPrimary()));
    }

    public string GetMediaPath(PartitionConfig partition) =>
        Path.Combine([.. partition.Paths, .. _config.Paths.Media.Paths]);

    public string GetCachePath(PartitionConfig partition) =>
        Path.Combine([.. partition.Paths, .. _config.Paths.Cache.Paths]);

    public string GetCacheDownloadPath(PartitionConfig partition) =>
        Path.Combine(
            [.. partition.Paths, .. _config.Paths.Tmp.Paths, .. _config.Paths.Tmp.Downloaded.Paths]
        );

    public string GetCacheFilePath(PartitionConfig partition)
    {
        string fileName = _dataStoreGuardService.RequireConfiguredFileName(
            _config.Paths.Cache.File
        );

        return Path.Combine(GetCachePath(partition), fileName);
    }

    public string GetPrimaryCacheFilePath() => GetCacheFilePath(_partition.GetPrimary());
}
