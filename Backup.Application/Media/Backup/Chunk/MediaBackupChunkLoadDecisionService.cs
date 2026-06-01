using Backup.Application.Media.Backup.Models;

namespace Backup.Application.Media.Backup;

public sealed class MediaBackupChunkLoadDecisionService : IMediaBackupChunkLoadDecisionService
{
    public MediaBackupChunkLoadDecision Decide(
        string? dataFileName,
        IReadOnlyList<int>? backupChunkIds
    )
    {
        if (string.IsNullOrWhiteSpace(dataFileName))
            return Create(MediaBackupChunkLoadAction.SkipAsNull, []);

        if (backupChunkIds is null)
            return Create(MediaBackupChunkLoadAction.SkipAsNull, []);

        if (backupChunkIds.Count == 0)
            return Create(MediaBackupChunkLoadAction.ReturnEmpty, []);

        List<MediaBackupChunkReadDescriptor> descriptors = [];

        for (int i = 0; i < backupChunkIds.Count; i++)
        {
            descriptors.Add(
                new MediaBackupChunkReadDescriptor { Index = i, ChunkId = backupChunkIds[i] }
            );
        }

        return Create(MediaBackupChunkLoadAction.Load, descriptors);
    }

    private static MediaBackupChunkLoadDecision Create(
        MediaBackupChunkLoadAction action,
        IReadOnlyList<MediaBackupChunkReadDescriptor> readDescriptors
    ) => new() { Action = action, ReadDescriptors = readDescriptors };
}
