namespace Backup.Application.Media;

public sealed class MediaStoragePathService(IMediaTempPathPolicyService mediaTempPathPolicyService)
    : IMediaStoragePathService
{
    private readonly IMediaTempPathPolicyService _mediaTempPathPolicyService =
        mediaTempPathPolicyService;

    public string BuildMediaRootPath(
        IEnumerable<string> partitionRootPaths,
        IReadOnlyList<string> mediaPaths
    )
    {
        string rootPath = SelectRequiredRootPath(partitionRootPaths);
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
        string rootPath = SelectRequiredRootPath(partitionRootPaths);
        return _mediaTempPathPolicyService.BuildDownloaderTempPath(
            rootPath,
            tmpPathSegments,
            downloaderPathSegments
        );
    }

    private static string SelectRequiredRootPath(IEnumerable<string> rootPaths)
    {
        string? rootPath = rootPaths.FirstOrDefault(path => !string.IsNullOrWhiteSpace(path));

        if (string.IsNullOrWhiteSpace(rootPath))
            throw new InvalidOperationException("No media root path is configured.");

        return rootPath;
    }
}
