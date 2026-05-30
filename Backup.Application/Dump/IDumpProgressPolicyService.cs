using Backup.Application.Dump.Models;

namespace Backup.Application.Dump;

public interface IDumpProgressPolicyService
{
    void AdvanceDirectoryIndex(DumpProgressState state);
}
