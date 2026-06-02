using Backup.Infrastructure.Media.Abstractions.Services;
using Backup.Infrastructure.Media.Models.Backup;
using Backup.Infrastructure.Logging;
using Backup.Infrastructure.Models.Utils;
using Backup.Infrastructure.Utility.Abstractions.Services;
using Microsoft.Extensions.Logging;

namespace Backup.Infrastructure.Media.Services;

internal sealed class MediaBackupChunkZipCoordinator(
    IZipWriterFactory zipWriterFactory,
    IMediaBackupZipEntryReaderIOService zipEntryReaderIoService
)
{
    private readonly IZipWriterFactory _zipWriterFactory = zipWriterFactory;
    private readonly IMediaBackupZipEntryReaderIOService _zipEntryReaderIoService =
        zipEntryReaderIoService;

    public async Task<IZipWriter?> OpenChunkZipRead(
        MediaBackupRuntime runtime,
        Chunk chunk,
        string stage
    )
    {
        Stream? zipFile = null;

        try
        {
            zipFile = await runtime.MediaBackupData.GetChunk(chunk);

            if (zipFile is null)
            {
                runtime.Logger.LogWarning(
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
            await runtime.RecoverCorruptChunk(chunk, stage, ex);
            return null;
        }
    }

    public async Task<IZipWriter?> OpenChunkZipWrite(
        MediaBackupRuntime runtime,
        Chunk chunk,
        string stage
    )
    {
        Stream? zipFile = null;

        try
        {
            zipFile = await runtime.MediaBackupData.GetChunk(chunk);

            if (zipFile is null)
            {
                runtime.Logger.LogError(
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
            await runtime.RecoverCorruptChunk(chunk, stage, ex);
            return null;
        }
    }

    public async Task<Dictionary<string, ZipEntry>?> ReadChunkEntries(
        MediaBackupRuntime runtime,
        Chunk chunk,
        string stage
    )
    {
        IZipWriter? zip = await OpenChunkZipRead(runtime, chunk, stage);

        if (zip is null)
            return null;

        try
        {
            runtime.Logger.LogInfo("read zip");
            runtime.Logger.LogInfo("reading entries");
            return _zipEntryReaderIoService.ReadEntriesByFullName(zip);
        }
        finally
        {
            runtime.Logger.LogInfo("disposing");
            zip.Dispose();
        }
    }

    public async Task<bool> MutateChunkZip(
        MediaBackupRuntime runtime,
        Chunk chunk,
        string stage,
        Func<IZipWriter, Task> mutation
    )
    {
        IZipWriter? zip = await OpenChunkZipWrite(runtime, chunk, stage);

        if (zip is null)
            return false;

        try
        {
            await mutation(zip);
            return true;
        }
        finally
        {
            runtime.Logger.LogInfo("disposing");
            zip.Dispose();
        }
    }
}
