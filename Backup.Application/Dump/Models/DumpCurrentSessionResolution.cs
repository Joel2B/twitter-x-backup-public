namespace Backup.Application.Dump.Models;

public sealed class DumpCurrentSessionResolution
{
    public required string Current { get; init; }
    public required bool ShouldPersist { get; init; }
}
