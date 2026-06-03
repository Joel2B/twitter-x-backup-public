namespace Backup.Application.Media;

public sealed class MediaTempPathPolicyService : IMediaTempPathPolicyService
{
    public string BuildDownloaderTempPath(
        string partitionRootPath,
        IReadOnlyList<string> tmpPathSegments,
        IReadOnlyList<string> downloaderPathSegments
    ) => Path.Combine([partitionRootPath, .. tmpPathSegments, .. downloaderPathSegments]);
}
