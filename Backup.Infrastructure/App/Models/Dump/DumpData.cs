namespace Backup.App.Models.Dump;

public class DumpData
{
    public string? Type { get; set; }
    public string? Cursor = null;
    public int Index { get; set; } = -1;
    public int IndexFile { get; set; } = -1;
    public int QueryCount { get; set; } = 0;
    public int Count { get; set; } = 0;
    public DateTime LastUpdate { get; set; } = DateTime.Now;
}
