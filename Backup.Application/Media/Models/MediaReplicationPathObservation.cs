namespace Backup.Application.Media.Models;

public sealed class MediaReplicationPathObservation
{
    public required string DownloadId { get; init; }
    public required string Url { get; init; }
    public required string Path { get; init; }
    public bool ExistsInSource { get; init; }
    public bool ExistsInTarget { get; init; }
}
