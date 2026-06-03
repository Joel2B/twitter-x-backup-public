namespace Backup.Application.Media;

public sealed class MediaDownloadPathPriorityPolicyService : IMediaDownloadPathPriorityPolicyService
{
    private static readonly HashSet<string> _videoExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".mp4",
        ".webm",
    };

    public int GetPriority(string path)
    {
        string extension = Path.GetExtension(path);
        return _videoExtensions.Contains(extension) ? 1 : 0;
    }
}
