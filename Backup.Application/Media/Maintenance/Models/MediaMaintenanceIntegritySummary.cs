namespace Backup.Application.Media.Maintenance.Models;

public sealed class MediaMaintenanceIntegritySummary
{
    public int NullCount { get; init; }
    public int SizeCount { get; init; }
    public int InvalidCount { get; init; }
}
