using Backup.Application.Dump.Models;

namespace Backup.Application.Dump;

public sealed class DumpSaveExecutionService(
    IDumpLifecycleService dumpLifecycleService,
    IDumpPathService dumpPathService
) : IDumpSaveExecutionService
{
    private readonly IDumpLifecycleService _dumpLifecycleService = dumpLifecycleService;
    private readonly IDumpPathService _dumpPathService = dumpPathService;

    public DumpSaveExecutionResult Execute(
        int index,
        int indexFile,
        int count,
        int queryCount,
        string cursor,
        DateTime now
    )
    {
        DumpProgressState directoryState = _dumpLifecycleService.AdvanceDirectory(
            index,
            indexFile,
            count,
            queryCount
        );
        DumpSaveProgressState saveState = _dumpLifecycleService.AdvanceSave(
            directoryState.IndexFile,
            cursor,
            now
        );

        return new DumpSaveExecutionResult
        {
            DirectoryState = directoryState,
            SaveState = saveState,
            FileName = _dumpPathService.BuildIndexFileName(saveState.IndexFile),
        };
    }
}
