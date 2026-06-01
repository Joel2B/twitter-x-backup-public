namespace Backup.Application.Media.Backup;

public interface IMediaBackupDirectPathQueueService
{
    IReadOnlyList<string> MergeAndNormalize(
        IEnumerable<string> existingPaths,
        IEnumerable<string> additionalPaths
    );

    IReadOnlyList<string> Normalize(IEnumerable<string> paths);
}
