using Backup.Application.IO;
using Backup.Application.Media.Maintenance;
using Backup.Infrastructure.Core.Abstractions.Partition;
using Backup.Infrastructure.Models.Config.Data;
using Backup.Infrastructure.Models.Config.Data.Media;
using Backup.Infrastructure.Models.Config.Downloads;

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
        List<PathConfig> cachePaths = _config
            .Cache.Where(cache => cache.Enabled && cache.Path is not null)
            .Select(cache => cache.Path!)
            .ToList();

        foreach (PartitionConfig item in _partition.GetPartitions())
        {
            if (_mediaCacheDirectoryPolicyService.ShouldCreateCacheDirectory(item.Type, item.Tags))
            {
                foreach (PathConfig cachePath in cachePaths)
                    Directory.CreateDirectory(GetCachePath(item, cachePath));
            }

            if (_mediaCacheDirectoryPolicyService.ShouldCreateMediaDirectory(item.Type))
                Directory.CreateDirectory(GetMediaPath(item));
        }

        Directory.CreateDirectory(GetCacheDownloadPath(_partition.GetPrimary()));
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

    public string GetPrimaryCacheFilePath(PathConfig pathConfig) =>
        GetCacheFilePath(_partition.GetPrimary(), pathConfig);

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
