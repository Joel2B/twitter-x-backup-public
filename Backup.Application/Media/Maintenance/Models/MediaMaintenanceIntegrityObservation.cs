namespace Backup.Application.Media.Maintenance.Models;

public sealed class MediaMaintenanceIntegrityObservation
{
    public required string CorrelationId { get; init; }
    public long? CacheFileSize { get; init; }
    public bool IsValidMediaFile { get; init; }
}
