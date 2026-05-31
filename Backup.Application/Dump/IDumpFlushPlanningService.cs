using Backup.Application.Dump.Models;

namespace Backup.Application.Dump;

public interface IDumpFlushPlanningService
{
    DumpFlushPlan Build(string? dumpType, string contextId, IEnumerable<string> postIds);
}
