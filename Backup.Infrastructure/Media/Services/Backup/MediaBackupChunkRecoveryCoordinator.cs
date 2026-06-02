using Backup.Application.Media.Backup;
using Backup.Application.Media.Backup.Models;
using Backup.Infrastructure.Media.Models.Backup;
using Microsoft.Extensions.Logging;

namespace Backup.Infrastructure.Media.Services;

internal sealed class MediaBackupChunkRecoveryCoordinator(
    IMediaBackupChunkFailureApplyService chunkFailureApplyService
)
{
    private readonly IMediaBackupChunkFailureApplyService _chunkFailureApplyService =
        chunkFailureApplyService;

    public async Task RecoverCorruptChunk(
        MediaBackupRuntime runtime,
        Chunk chunk,
        string stage,
        Exception ex
    )
    {
        runtime.Logger.LogError(
            ex,
            "chunk {chunk} zip failed ({stage}); deleting and scheduling rebuild",
            chunk.Id,
            stage
        );

        await runtime.MediaBackupData.DeleteChunk(chunk);

        IReadOnlyList<MediaBackupChunkEntryState> resetStates =
            _chunkFailureApplyService.ApplyForCorruptChunk(runtime.BuildChunkEntryStates(chunk.Data));
        runtime.ApplyChunkEntryStates(chunk, resetStates);

        await runtime.MediaBackupData.Save([chunk]);
    }

    public async Task RecoverApplyFailure(MediaBackupRuntime runtime, Chunk chunk)
    {
        await runtime.MediaBackupData.DeleteChunk(chunk);

        IReadOnlyList<MediaBackupChunkEntryState> resetStates =
            _chunkFailureApplyService.ApplyForApplyFailure(runtime.BuildChunkEntryStates(chunk.Data));
        runtime.ApplyChunkEntryStates(chunk, resetStates);

        await runtime.MediaBackupData.Save([chunk]);
    }
}
