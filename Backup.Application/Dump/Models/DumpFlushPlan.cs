namespace Backup.Application.Dump.Models;

public sealed class DumpFlushPlan
{
    public required string SourceId { get; init; }

    public required IReadOnlySet<string> NewPostIds { get; init; }
}
