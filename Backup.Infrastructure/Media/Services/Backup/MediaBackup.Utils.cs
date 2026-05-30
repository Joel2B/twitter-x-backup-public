using Backup.Infrastructure.Logging;
using Backup.Infrastructure.Media.Abstractions.Services;
using Backup.Infrastructure.Media.Models;
using Backup.Infrastructure.Media.Models.Backup;
using Microsoft.Extensions.Logging;

namespace Backup.Infrastructure.Media.Services;

public partial class MediaBackup
{
    private async Task ShowInfoChunks()
    {
        _logger.LogInfo("{id,-3} {paths,-10} {size}", "id", "paths", "size (GiB)");

        foreach (var kvp in _chunks)
        {
            if (kvp.Value.Data.Count == 0)
                continue;

            long size = 0;

            foreach (ChunkData chunkData in _chunks[kvp.Key].Data)
            {
                MediaCacheEntry? cache = await MediaData.GetCache(chunkData.Path);

                if (cache is not null)
                    size += cache.Size?.File ?? 0;
            }

            _logger.LogInformation(
                "{id,-3} {paths,-10} {size}",
                kvp.Key,
                kvp.Value.Data.Count,
                Math.Round(size / 1024m / 1024m / 1024m, 2, MidpointRounding.ToZero)
            );
        }
    }
}
