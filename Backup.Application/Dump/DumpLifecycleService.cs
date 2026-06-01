using Backup.Application.Dump.Models;

namespace Backup.Application.Dump;

public sealed class DumpLifecycleService(
    IDumpSessionNamingPolicyService dumpSessionNamingPolicyService,
    IDumpContextGuardService dumpContextGuardService,
    IDumpProgressPolicyService dumpProgressPolicyService
) : IDumpLifecycleService
{
    private readonly IDumpSessionNamingPolicyService _dumpSessionNamingPolicyService =
        dumpSessionNamingPolicyService;
    private readonly IDumpContextGuardService _dumpContextGuardService = dumpContextGuardService;
    private readonly IDumpProgressPolicyService _dumpProgressPolicyService =
        dumpProgressPolicyService;

    public DumpCurrentSessionResolution ResolveCurrentSession(string? current, DateTime now)
    {
        string resolved = current ?? _dumpSessionNamingPolicyService.CreateCurrentSessionName(now);

        return new()
        {
            Current = resolved,
            ShouldPersist = !string.Equals(current, resolved, StringComparison.Ordinal),
        };
    }

    public DumpSessionCloseResolution ResolveSessionClose(string? current) =>
        new() { Current = null, ShouldPersist = !string.IsNullOrWhiteSpace(current) };

    public DumpDataInitialization CreateInitialData(int count, object? queryCountRaw)
    {
        int queryCount = ToInt(queryCountRaw);

        return new() { Count = count, QueryCount = queryCount };
    }

    public string ResolveType(string? current, string contextId, string? existingType) =>
        _dumpContextGuardService.ResolveType(current, contextId, existingType);

    public DumpProgressState AdvanceDirectory(int index, int indexFile, int count, int queryCount)
    {
        DumpProgressState state = new()
        {
            Index = index,
            IndexFile = indexFile,
            Count = count,
            QueryCount = queryCount,
        };

        _dumpProgressPolicyService.AdvanceDirectoryIndex(state);
        return state;
    }

    public DumpSaveProgressState AdvanceSave(int indexFile, string cursor, DateTime now) =>
        new()
        {
            IndexFile = indexFile + 1,
            Cursor = cursor,
            LastUpdate = now,
        };

    private static int ToInt(object? value)
    {
        if (value is null)
            return 0;

        return value switch
        {
            int intValue => intValue,
            long longValue when longValue >= int.MinValue && longValue <= int.MaxValue =>
                (int)longValue,
            _ => int.TryParse(value.ToString(), out int parsed) ? parsed : 0,
        };
    }
}
