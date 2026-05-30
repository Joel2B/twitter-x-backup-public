namespace Backup.Application.Bulk.Models;

public sealed class BulkPhase1Options
{
    public int UsersPerCycle { get; set; }
    public int SavePerAction { get; set; }
    public int ApiPerCycle { get; set; }
    public int MediaPerApi { get; set; }
    public int MaxCountPost { get; set; }
    public int ApiRetryCount { get; set; }
}
