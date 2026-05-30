namespace Backup.Application.Dump.Models;

public sealed class DumpProgressState
{
    public int Index { get; set; } = -1;
    public int IndexFile { get; set; } = -1;
    public int QueryCount { get; set; }
    public int Count { get; set; }
}
