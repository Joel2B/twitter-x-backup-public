using Backup.Application.Dump.Models;

namespace Backup.Application.Dump;

public interface IDumpLifecycleService
{
    DumpCurrentSessionResolution ResolveCurrentSession(string? current, DateTime now);
    DumpSessionCloseResolution ResolveSessionClose(string? current);
    DumpDataInitialization CreateInitialData(int count, object? queryCountRaw);
    string ResolveType(string? current, string contextId, string? existingType);
    DumpProgressState AdvanceDirectory(int index, int indexFile, int count, int queryCount);
    DumpSaveProgressState AdvanceSave(int indexFile, string cursor, DateTime now);
}
