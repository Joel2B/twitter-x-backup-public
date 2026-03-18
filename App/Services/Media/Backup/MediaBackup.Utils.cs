using Backup.App.Extensions;
using Backup.App.Interfaces.Services.Media;
using Backup.App.Models.Media;
using Backup.App.Models.Media.Backup;
using Microsoft.Extensions.Logging;

namespace Backup.App.Services.Media;

public partial class MediaBackup : IMediaBackup
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
                Cache? cache = await _mediaData.GetCache(chunkData.Path);

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
