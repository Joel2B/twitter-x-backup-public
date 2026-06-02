using Backup.Application.Core;
using Backup.Application.Dump;
using Backup.Application.Dump.Models;
using Backup.Infrastructure.Dump.Abstractions.Services;
using Backup.Infrastructure.Models.Config.Api;
using Backup.Infrastructure.Models.Dump;
using Backup.Infrastructure.Posts.Models.Stored;

namespace Backup.Infrastructure.Dump.Data;

internal sealed class LocalDumpDataSaveCoordinator(
    IDumpSaveExecutionService dumpSaveExecutionService,
    IDateTimeProvider dateTimeProvider,
    IDumpPersistenceIOService dumpPersistenceIOService,
    LocalDumpDataSessionPathResolver sessionPathResolver,
    LocalDumpDataStateCoordinator stateCoordinator,
    LocalDumpDataReplicationCoordinator replicationCoordinator
)
{
    private readonly IDumpSaveExecutionService _dumpSaveExecutionService = dumpSaveExecutionService;
    private readonly IDateTimeProvider _dateTimeProvider = dateTimeProvider;
    private readonly IDumpPersistenceIOService _dumpPersistenceIOService = dumpPersistenceIOService;
    private readonly LocalDumpDataSessionPathResolver _sessionPathResolver = sessionPathResolver;
    private readonly LocalDumpDataStateCoordinator _stateCoordinator = stateCoordinator;
    private readonly LocalDumpDataReplicationCoordinator _replicationCoordinator =
        replicationCoordinator;

    public async Task Save(
        string response,
        List<Post> posts,
        string cursor,
        ApiContext context,
        DumpData dumpData,
        CancellationToken cancellationToken = default
    )
    {
        DumpSaveExecutionResult saveExecution = _dumpSaveExecutionService.Execute(
            dumpData.Index,
            dumpData.IndexFile,
            dumpData.Count,
            dumpData.QueryCount,
            cursor,
            _dateTimeProvider.Now
        );

        dumpData.Index = saveExecution.DirectoryState.Index;
        dumpData.IndexFile = saveExecution.SaveState.IndexFile;

        string indexPath = await _sessionPathResolver.GetIndexPath(
            context,
            dumpData.Index,
            cancellationToken
        );
        string apiPath = await _sessionPathResolver.GetApiPath(
            context,
            dumpData.Index,
            cancellationToken
        );

        Directory.CreateDirectory(indexPath);
        Directory.CreateDirectory(apiPath);

        string indexFullPath = Path.Combine(indexPath, saveExecution.FileName);
        string apiFullPath = Path.Combine(apiPath, saveExecution.FileName);

        await _dumpPersistenceIOService.WritePostsIndex(indexFullPath, posts, cancellationToken);
        await _dumpPersistenceIOService.WriteApiResponse(apiFullPath, response, cancellationToken);

        dumpData.Cursor = saveExecution.SaveState.Cursor;
        dumpData.LastUpdate = saveExecution.SaveState.LastUpdate;

        await _stateCoordinator.Save(context, dumpData, cancellationToken);
        await _replicationCoordinator.Replicate(context, cancellationToken);
    }
}
