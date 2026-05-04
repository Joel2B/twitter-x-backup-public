using Backup.App.Interfaces.Services.Media;
using Backup.App.Interfaces.Services.UtilsService;
using Backup.App.Models.Media.Backup;
using Microsoft.Extensions.Logging;

namespace Backup.App.Services.Media;

public partial class MediaBackup : IMediaBackup
{
    private async Task<IZipWriter?> OpenChunkZipRead(Chunk chunk, string stage)
    {
        Stream? zipFile = null;

        try
        {
            zipFile = await _mediaBackup.GetChunk(chunk);

            if (zipFile is null)
            {
                _logger.LogWarning(
                    "chunk {chunk} zip missing while reading ({stage})",
                    chunk.Id,
                    stage
                );
                return null;
            }

            return _zipWriterFactory.Open(zipFile);
        }
        catch (Exception ex)
        {
            zipFile?.Dispose();
            await RecoverCorruptChunk(chunk, stage, ex);
            return null;
        }
    }

    private async Task<IZipWriter?> OpenChunkZipWrite(Chunk chunk, string stage)
    {
        Stream? zipFile = null;

        try
        {
            zipFile = await _mediaBackup.GetChunk(chunk);

            if (zipFile is null)
            {
                _logger.LogError(
                    "chunk {chunk} zip stream unavailable for write ({stage})",
                    chunk.Id,
                    stage
                );
                return null;
            }

            return _zipWriterFactory.Create(zipFile);
        }
        catch (Exception ex)
        {
            zipFile?.Dispose();
            await RecoverCorruptChunk(chunk, stage, ex);
            return null;
        }
    }

    private async Task RecoverCorruptChunk(Chunk chunk, string stage, Exception ex)
    {
        _logger.LogError(
            ex,
            "chunk {chunk} zip failed ({stage}); deleting and scheduling rebuild",
            chunk.Id,
            stage
        );

        await _mediaBackup.DeleteChunk(chunk);

        foreach (ChunkData item in chunk.Data)
        {
            item.Hash = null;
            item.FileSize = null;
            item.Crc32 = null;
        }

        await _mediaBackup.Save([chunk]);
    }
}
