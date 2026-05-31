using Backup.Application.Dump.Models;

namespace Backup.Application.Dump;

public interface IDumpReplicationPlanningService
{
    DumpReplicationPlan Plan(
        string primaryRootPath,
        string currentPath,
        IEnumerable<string> replicaRootPaths
    );
}
