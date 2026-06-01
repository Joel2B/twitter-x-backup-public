namespace Backup.Application.Media.Backup;

public sealed class MediaBackupDirectPathQueueService : IMediaBackupDirectPathQueueService
{
    public IReadOnlyList<string> MergeAndNormalize(
        IEnumerable<string> existingPaths,
        IEnumerable<string> additionalPaths
    ) => Normalize(existingPaths.Concat(additionalPaths));

    public IReadOnlyList<string> Normalize(IEnumerable<string> paths)
    {
        HashSet<string> seen = new(StringComparer.OrdinalIgnoreCase);
        List<string> ordered = [];

        foreach (string path in paths)
        {
            if (!seen.Add(path))
                continue;

            ordered.Add(path);
        }

        return ordered;
    }
}
