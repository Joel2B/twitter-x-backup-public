namespace Backup.Application.Dump.Models;

public sealed class DumpReplicationPlan
{
    public required string RelativePath { get; init; }
    public required IReadOnlyList<string> TargetPaths { get; init; }
}
