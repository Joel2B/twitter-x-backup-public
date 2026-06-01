using Backup.Application.Dump.Models;

namespace Backup.Application.Dump;

public interface IDumpSaveExecutionService
{
    DumpSaveExecutionResult Execute(
        int index,
        int indexFile,
        int count,
        int queryCount,
        string cursor,
        DateTime now
    );
}
