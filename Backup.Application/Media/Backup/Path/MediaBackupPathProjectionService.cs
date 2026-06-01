namespace Backup.Application.Media.Backup;

public sealed class MediaBackupPathProjectionService : IMediaBackupPathProjectionService
{
    public string ToArchivePath(string path) => path.Replace('\\', '/');

    public IReadOnlyList<string> ToArchivePaths(IEnumerable<string> paths) =>
        paths.Select(ToArchivePath).ToList();
}
