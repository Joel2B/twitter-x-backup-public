namespace Backup.Application.Media.Backup;

public interface IMediaBackupPathProjectionService
{
    string ToArchivePath(string path);
    IReadOnlyList<string> ToArchivePaths(IEnumerable<string> paths);
}
