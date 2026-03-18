namespace Backup.App.Models.Config;

public class Bulk
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

public class BulkData : Downloads.Path
{
    public bool Prune { get; set; }
    public required Downloads.Path Bulk { get; set; }
    public required Downloads.Path Sources { get; set; }
}
