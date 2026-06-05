namespace Backup.Application.Media.Backup;

public static class MediaBackupPathProjection
{
    public static string ToArchivePath(string path) => path.Replace('\\', '/');

    public static IReadOnlyList<string> ToArchivePaths(IEnumerable<string> paths) =>
        paths.Select(ToArchivePath).ToList();
}
