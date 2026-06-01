namespace Backup.Application.Media.Maintenance.Models;

public sealed class MediaMaintenanceIntegrityItemResult
{
    public required string CorrelationId { get; init; }
    public required bool Remove { get; init; }
    public required MediaMaintenanceIntegrityEvaluation Evaluation { get; init; }
}
