namespace Backup.Application.Media.Maintenance.Models;

public sealed class MediaMaintenanceIntegrityTarget
{
    public required string CorrelationId { get; init; }
    public required string Path { get; init; }
}
