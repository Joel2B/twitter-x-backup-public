using Backup.Application.Dump.Models;

namespace Backup.Application.Dump;

public sealed class DumpProgressPolicyService : IDumpProgressPolicyService
{
    public string EnsureCurrent(string? current, DateTime now) =>
        string.IsNullOrWhiteSpace(current) ? now.ToString("yyyy.MM.dd-HH.mm.ss") : current;

    public void AdvanceDirectoryIndex(DumpProgressState state)
    {
        int files = state.Count / state.QueryCount - 1;

        if (state.IndexFile != files && state.Index != -1)
            return;

        state.Index++;
        state.IndexFile = -1;
    }
}
