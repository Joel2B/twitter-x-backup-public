using Backup.Infrastructure.Logging;
using Backup.Infrastructure.Interfaces.Services.Media;
using Backup.Infrastructure.Interfaces.Services.UtilsService;
using Backup.Infrastructure.Models.Media.Backup;
using Backup.Infrastructure.Models.Utils;
using Microsoft.Extensions.Logging;

namespace Backup.Infrastructure.Services.Media;

public partial class MediaBackup
{
    private async Task SetFileSizes()
    {
        _logger.LogInformation("setting file sizes");

        foreach (var kvp in _chunks)
        {
            bool isNull = kvp.Value.Data.Any(o =>
                o.FileSize is null || o.FileSize == 0 || o.Crc32 is null
            );

            if (!isNull)
                continue;

            _logger.LogInformation("processing chunk {chunk}", kvp.Key);
            IZipWriter? zip = await OpenChunkZipRead(kvp.Value, "set-file-sizes");

            if (zip is null)
                continue;

            Dictionary<string, ZipEntry> entries;

            try
            {
                _logger.LogInfo("read zip");
                _logger.LogInfo("reading entries");
                entries = zip.GetEntries().ToDictionary(o => o.FullName);
            }
            finally
            {
                _logger.LogInfo("disposing");
                zip.Dispose();
            }

            _logger.LogInfo("updating data");

            foreach (ChunkData item in kvp.Value.Data)
            {
                entries.TryGetValue(item.Path.Replace('\\', '/'), out ZipEntry? value);

                if (value is null)
                    continue;

                item.FileSize ??= value?.FileSize;
                item.Crc32 ??= value?.Crc32;
            }

            _logger.LogInfo("saving chunk");
            await _mediaBackupData.Save([kvp.Value]);

            _logger.LogInformation("chunk {chunk} processed", kvp.Key);
        }
    }
}

