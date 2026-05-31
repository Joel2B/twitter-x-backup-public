using Backup.Application.Dump.Models;

namespace Backup.Application.Dump;

public sealed class DumpFlushPlanningService : IDumpFlushPlanningService
{
    public DumpFlushPlan Build(string? dumpType, string contextId, IEnumerable<string> postIds) =>
        new()
        {
            SourceId = string.IsNullOrWhiteSpace(dumpType) ? contextId : dumpType,
            NewPostIds = postIds.ToHashSet(StringComparer.Ordinal),
        };
}
