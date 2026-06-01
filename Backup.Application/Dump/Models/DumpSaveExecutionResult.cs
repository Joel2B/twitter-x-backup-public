namespace Backup.Application.Dump.Models;

public sealed class DumpSaveExecutionResult
{
    public required DumpProgressState DirectoryState { get; init; }

    public required DumpSaveProgressState SaveState { get; init; }

    public required string FileName { get; init; }
}
