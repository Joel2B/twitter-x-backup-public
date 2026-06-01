namespace Backup.Application.Dump.Models;

public sealed class DumpSaveProgressState
{
    public int IndexFile { get; init; }
    public required string Cursor { get; init; }
    public required DateTime LastUpdate { get; init; }
}
