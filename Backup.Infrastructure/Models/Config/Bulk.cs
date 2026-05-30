using Backup.Infrastructure.Models.Config.Downloads;

namespace Backup.Infrastructure.Models.Config;

public class BulkConfig
{
    public required bool Enabled { get; set; }
    public int UsersPerCycle { get; set; }
    public int SavePerAction { get; set; }
    public int UsersPerPhase2 { get; set; }
    public int MaxCountPostPhase2 { get; set; }
    public int ApiPerCycle { get; set; }
    public int MediaPerApi { get; set; }
    public int MaxCountPost { get; set; }
    public int ApiRetryCount { get; set; }
}
