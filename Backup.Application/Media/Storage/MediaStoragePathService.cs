namespace Backup.Application.Media;

public sealed class MediaStoragePathService(
    IMediaPathSelectionService mediaPathSelectionService,
    IMediaTempPathPolicyService mediaTempPathPolicyService
) : IMediaStoragePathService
{
    private readonly IMediaPathSelectionService _mediaPathSelectionService =
        mediaPathSelectionService;
    private readonly IMediaTempPathPolicyService _mediaTempPathPolicyService =
        mediaTempPathPolicyService;

    public string BuildMediaRootPath(
        IEnumerable<string> partitionRootPaths,
        IReadOnlyList<string> mediaPaths
    )
    {
        string rootPath = _mediaPathSelectionService.SelectRequiredRootPath(partitionRootPaths);
        return Path.Combine([rootPath, .. mediaPaths]);
    }

    public string BuildMediaLogPath(string mediaRootPath, IReadOnlyList<string> logPaths) =>
        Path.Combine([mediaRootPath, .. logPaths]);

    public string BuildMediaErrorPath(string mediaRootPath, IReadOnlyList<string> errorPaths) =>
        Path.Combine([mediaRootPath, .. errorPaths]);

    public string BuildDownloaderTempPath(
        IEnumerable<string> partitionRootPaths,
        IReadOnlyList<string> tmpPathSegments,
        IReadOnlyList<string> downloaderPathSegments
    )
    {
        string rootPath = _mediaPathSelectionService.SelectRequiredRootPath(partitionRootPaths);
        return _mediaTempPathPolicyService.BuildDownloaderTempPath(
            rootPath,
            tmpPathSegments,
            downloaderPathSegments
        );
    }
}
