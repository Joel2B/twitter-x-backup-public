namespace Backup.Application.Media;

public sealed class MediaPathSelectionService : IMediaPathSelectionService
{
    public string SelectRequiredRootPath(IEnumerable<string> rootPaths)
    {
        string? rootPath = rootPaths.FirstOrDefault(path => !string.IsNullOrWhiteSpace(path));

        if (string.IsNullOrWhiteSpace(rootPath))
            throw new InvalidOperationException("No media root path is configured.");

        return rootPath;
    }
}
