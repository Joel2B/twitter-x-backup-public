using Backup.Application.IO;
using Backup.Application.Media.Maintenance;
using Backup.Infrastructure.Core.Abstractions.Partition;
using Backup.Infrastructure.Models.Config.Data;
using Backup.Infrastructure.Models.Config.Data.Media;
using Backup.Infrastructure.Models.Config.Downloads;

namespace Backup.Infrastructure.Media.Data;

internal sealed class LocalMediaCachePathLayout(
    StorageMedia config,
    IPartition storagePartition,
    IReadOnlyList<MediaCacheTargetRuntime> cacheTargets,
    IDataStoreGuardService dataStoreGuardService,
    IMediaCacheDirectoryPolicyService mediaCacheDirectoryPolicyService
)
{
    private readonly StorageMedia _config = config;
    private readonly IPartition _storagePartition = storagePartition;
    private readonly IReadOnlyList<MediaCacheTargetRuntime> _cacheTargets = cacheTargets;
    private readonly IDataStoreGuardService _dataStoreGuardService = dataStoreGuardService;
    private readonly IMediaCacheDirectoryPolicyService _mediaCacheDirectoryPolicyService =
        mediaCacheDirectoryPolicyService;

    public void EnsureDirectories()
    {
        foreach (PartitionConfig item in _storagePartition.GetPartitions())
            if (_mediaCacheDirectoryPolicyService.ShouldCreateMediaDirectory(item.Type))
                Directory.CreateDirectory(GetMediaPath(item));

        foreach (MediaCacheTargetRuntime target in _cacheTargets)
        {
            foreach (PartitionConfig partition in target.Partitions)
            {
                if (
                    target.Path is not null
                    && _mediaCacheDirectoryPolicyService.ShouldCreateCacheDirectory(
                        partition.Type,
                        partition.Tags
                    )
                )
                    Directory.CreateDirectory(GetCachePath(partition, target.Path));
            }

            Directory.CreateDirectory(GetCacheDownloadPath(target.PrimaryPartition));
            Directory.CreateDirectory(
                GetIncrementalCacheDirectory(target.PrimaryPartition, target.Key)
            );
        }
    }

    public string GetMediaPath(PartitionConfig partition) =>
        Path.Combine([.. partition.Paths, .. _config.Paths.Media.Paths]);

    public string GetCachePath(PartitionConfig partition, PathConfig pathConfig) =>
        Path.Combine([.. partition.Paths, .. pathConfig.Paths]);

    public string GetCacheDownloadPath(PartitionConfig partition) =>
        Path.Combine(
            [.. partition.Paths, .. _config.Paths.Tmp.Paths, .. _config.Paths.Tmp.Downloaded.Paths]
        );

    public string GetCacheFilePath(PartitionConfig partition, PathConfig pathConfig)
    {
        string fileName = _dataStoreGuardService.RequireConfiguredFileName(pathConfig.File);

        return Path.Combine(GetCachePath(partition, pathConfig), fileName);
    }

    public string GetIncrementalCacheDirectory(PartitionConfig partition, string cacheKey) =>
        Path.Combine(GetCacheDownloadPath(partition), "cache", SanitizeKey(cacheKey));

    public string GetVirtualPrimaryCacheFilePath(PartitionConfig partition, string cacheKey) =>
        Path.Combine(GetIncrementalCacheDirectory(partition, cacheKey), "primary.cache");

    private static string SanitizeKey(string value)
    {
        char[] invalidChars = Path.GetInvalidFileNameChars();
        char[] buffer = value
            .Select(ch => invalidChars.Contains(ch) ? '_' : char.ToLowerInvariant(ch))
            .ToArray();

        return new string(buffer);
    }
}
