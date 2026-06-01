namespace Backup.Application.Dump;

public interface IDumpContextEligibilityService
{
    bool ShouldLoadDumpData(int requestedCount);
}
