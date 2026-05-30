namespace Backup.Application.Media.Models;

public sealed class MediaProcessingResult
{
    public required List<MediaDownload> All { get; set; }
    public required List<MediaDownload> Filtered { get; set; }
}
