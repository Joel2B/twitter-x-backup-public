namespace Backup.Application.Media;

public interface IMediaStoragePathService
{
    string BuildMediaRootPath(IEnumerable<string> partitionRootPaths, IReadOnlyList<string> mediaPaths);
    string BuildMediaLogPath(string mediaRootPath, IReadOnlyList<string> logPaths);
    string BuildMediaErrorPath(string mediaRootPath, IReadOnlyList<string> errorPaths);
    string BuildDownloaderTempPath(
        IEnumerable<string> partitionRootPaths,
        IReadOnlyList<string> tmpPathSegments,
        IReadOnlyList<string> downloaderPathSegments
    );
}
