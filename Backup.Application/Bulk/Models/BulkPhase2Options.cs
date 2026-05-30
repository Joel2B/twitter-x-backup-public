namespace Backup.Application.Bulk.Models;

public sealed class BulkPhase2Options
{
    public int UsersPerPhase2 { get; set; }
    public int SavePerAction { get; set; }
    public int MediaPerApi { get; set; }
    public int MaxCountPostPhase2 { get; set; }
    public int ApiRetryCount { get; set; }
}
