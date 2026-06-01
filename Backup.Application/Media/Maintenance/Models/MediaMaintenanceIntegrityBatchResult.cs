namespace Backup.Application.Media.Maintenance.Models;

public sealed class MediaMaintenanceIntegrityBatchResult
{
    public required IReadOnlyList<MediaMaintenanceIntegrityItemResult> Items { get; init; }
    public required MediaMaintenanceIntegritySummary Summary { get; init; }
}
