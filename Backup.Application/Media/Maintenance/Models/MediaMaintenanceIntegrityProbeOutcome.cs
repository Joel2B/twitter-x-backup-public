namespace Backup.Application.Media.Maintenance.Models;

public sealed class MediaMaintenanceIntegrityProbeOutcome
{
    public required string CorrelationId { get; init; }
    public long? CacheFileSize { get; init; }
    public bool IsValidMediaFile { get; init; }
}
