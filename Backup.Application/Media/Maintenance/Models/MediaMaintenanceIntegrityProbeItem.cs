namespace Backup.Application.Media.Maintenance.Models;

public sealed class MediaMaintenanceIntegrityProbeItem
{
    public required string CorrelationId { get; init; }
    public required string Path { get; init; }
    public long? CacheFileSize { get; init; }
    public bool ShouldProbe { get; init; }
}
