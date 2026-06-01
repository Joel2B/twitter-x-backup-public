namespace Backup.Application.Dump.Models;

public sealed class DumpSessionCloseResolution
{
    public string? Current { get; init; }
    public bool ShouldPersist { get; init; }
}
