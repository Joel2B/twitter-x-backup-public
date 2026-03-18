using Backup.App.Extensions;
using Backup.App.Interfaces.Services.Media;
using Backup.App.Interfaces.Services.UtilsService;
using Backup.App.Models.Media.Backup;
using Backup.App.Models.Utils;
using Microsoft.Extensions.Logging;

namespace Backup.App.Services.Media;

public partial class MediaBackup : IMediaBackup
{
    public async Task SetFileSizes()
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
            Stream? zipFile = await _mediaBackup.GetChunk(kvp.Value);

            if (zipFile is null)
            {
                _logger.LogError("error in GetChunk");
                continue;
            }

            _logger.LogInfo("read zip");
            IZipWriter zip = _zipWriterFactory.Open(zipFile);

            _logger.LogInfo("reading entries");
            Dictionary<string, ZipEntry> entries = zip.GetEntries().ToDictionary(o => o.FullName);

            _logger.LogInfo("disposing");
            zip?.Dispose();
            zipFile?.Dispose();

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
            await _mediaBackup.Save([kvp.Value]);

            _logger.LogInformation("chunk {chunk} processed", kvp.Key);
        }
    }
}
