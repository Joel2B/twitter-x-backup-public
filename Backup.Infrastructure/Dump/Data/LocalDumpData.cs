using Backup.Application.Dump.Models;
using Backup.Application.IO;
using Backup.Infrastructure.Dump.Abstractions.Data;
using Backup.Infrastructure.Models.Config.Api;
using Backup.Infrastructure.Models.Dump;
using Backup.Infrastructure.Posts.Abstractions.Data;
using Backup.Infrastructure.Posts.Models.Stored;
using Microsoft.Extensions.Logging;

namespace Backup.Infrastructure.Dump.Data;

public class LocalDumpData : IDumpDataStore
{
    public string? Id { get; set; }
    public bool IsDefault { get; set; }

    private readonly ILogger<LocalDumpData> _logger;
    private readonly IDataStoreGuardService _dataStoreGuardService;
    private readonly LocalDumpDataLoadCoordinator _loadCoordinator;
    private readonly LocalDumpDataSaveCoordinator _saveCoordinator;
    private readonly LocalDumpDataFlushCoordinator _flushCoordinator;

    private DumpData? _dumpData;
    private DumpData Data =>
        _dataStoreGuardService.RequireInitialized(_dumpData, "Dump data not initialized");

    internal LocalDumpData(
        ILogger<LocalDumpData> logger,
        IDataStoreGuardService dataStoreGuardService,
        LocalDumpDataLoadCoordinator loadCoordinator,
        LocalDumpDataSaveCoordinator saveCoordinator,
        LocalDumpDataFlushCoordinator flushCoordinator
    )
    {
        _logger = logger;
        _dataStoreGuardService = dataStoreGuardService;
        _loadCoordinator = loadCoordinator;
        _saveCoordinator = saveCoordinator;
        _flushCoordinator = flushCoordinator;
    }

    public Task Setup() => Task.CompletedTask;

    public async Task<DumpData?> GetData(
        ApiContext context,
        CancellationToken cancellationToken = default
    )
    {
        _dumpData = await _loadCoordinator.Load(context, cancellationToken);
        return _dumpData;
    }

    public async Task Save(
        string response,
        List<Post> posts,
        string cursor,
        ApiContext context,
        CancellationToken cancellationToken = default
    ) => await _saveCoordinator.Save(response, posts, cursor, context, Data, cancellationToken);

    public async Task Flush(
        IPostDomainData postData,
        string userId,
        ApiContext context,
        CancellationToken cancellationToken = default
    )
    {
        _logger.LogInformation("dumping data");

        DumpFlushOrchestrationResult orchestration = await _flushCoordinator.Flush(
            postData,
            userId,
            context,
            Data,
            cancellationToken
        );

        _logger.LogInformation(
            "{posts} posts loaded from dump",
            orchestration.FlushResult.LoadedCount
        );
        _logger.LogInformation("{posts} posts deleted", orchestration.FlushResult.DeletedCount);
    }
}
