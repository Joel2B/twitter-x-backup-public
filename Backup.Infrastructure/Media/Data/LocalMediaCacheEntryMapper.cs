using Backup.Application.Media.Maintenance.Models;
using Backup.Infrastructure.Media.Models;

namespace Backup.Infrastructure.Media.Data;

internal static class LocalMediaCacheEntryMapper
{
    public static MediaCacheEntry ToCacheEntry(MediaCacheEntryState state) =>
        new()
        {
            Path = state.Path,
            PartitionId = state.PartitionId,
            Size = new MediaCacheSize
            {
                Stream = state.StreamSizeBytes,
                File = state.FileSizeBytes,
            },
        };

    public static MediaCacheStoredEntry ToStoredEntry(MediaCacheEntry entry) =>
        new()
        {
            Path = entry.Path,
            PartitionId = entry.PartitionId,
            StreamSizeBytes = entry.Size?.Stream,
            FileSizeBytes = entry.Size?.File,
        };
}
