namespace Backup.Application.Media.Maintenance.Models;

public sealed class MediaMaintenanceIntegrityEvaluation
{
    public required bool Remove { get; init; }
    public required int NullCountIncrement { get; init; }
    public required int SizeCountIncrement { get; init; }
    public required int InvalidCountIncrement { get; init; }
}
