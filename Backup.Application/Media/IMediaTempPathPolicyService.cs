namespace Backup.Application.Media;

public interface IMediaTempPathPolicyService
{
    string BuildDownloaderTempPath(
        string partitionRootPath,
        IReadOnlyList<string> tmpPathSegments,
        IReadOnlyList<string> downloaderPathSegments
    );
}
