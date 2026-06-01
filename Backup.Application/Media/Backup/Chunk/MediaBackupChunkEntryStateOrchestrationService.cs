using Backup.Application.Media.Backup.Models;

namespace Backup.Application.Media.Backup;

public sealed class MediaBackupChunkEntryStateOrchestrationService(
    IMediaBackupChunkEntryStateMutationService mediaBackupChunkEntryStateMutationService
) : IMediaBackupChunkEntryStateOrchestrationService
{
    private readonly IMediaBackupChunkEntryStateMutationService _mediaBackupChunkEntryStateMutationService =
        mediaBackupChunkEntryStateMutationService;

    public IReadOnlyList<MediaBackupChunkEntryState> BuildStates(
        IEnumerable<MediaBackupChunkEntryRecord> entries
    ) =>
        _mediaBackupChunkEntryStateMutationService.BuildStates(
            entries.Select(entry => new MediaBackupChunkEntryMutationInput
            {
                Path = entry.Path,
                Hash = entry.Hash,
                FileSize = entry.FileSize,
                Crc32 = entry.Crc32,
            })
        );

    public IReadOnlyList<MediaBackupChunkEntryRecord> ApplyStates(
        IEnumerable<MediaBackupChunkEntryRecord> entries,
        IEnumerable<MediaBackupChunkEntryState> states
    )
    {
        IReadOnlyList<MediaBackupChunkEntryMutationInput> updated =
            _mediaBackupChunkEntryStateMutationService.ApplyStates(
                entries.Select(entry => new MediaBackupChunkEntryMutationInput
                {
                    Path = entry.Path,
                    Hash = entry.Hash,
                    FileSize = entry.FileSize,
                    Crc32 = entry.Crc32,
                }),
                states
            );

        return updated
            .Select(item => new MediaBackupChunkEntryRecord
            {
                Path = item.Path,
                Hash = item.Hash,
                FileSize = item.FileSize,
                Crc32 = item.Crc32,
            })
            .ToList();
    }
}
