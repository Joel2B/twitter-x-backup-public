namespace Backup.Application.Dump;

public sealed class DumpContextEligibilityService : IDumpContextEligibilityService
{
    public bool ShouldLoadDumpData(int requestedCount) => requestedCount == -1;
}
