using Backup.Application.Dump.Models;

namespace Backup.Application.Dump;

public interface IDumpProgressPolicyService
{
    string EnsureCurrent(string? current, DateTime now);
    void AdvanceDirectoryIndex(DumpProgressState state);
}
