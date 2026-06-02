using Backup.Application.Dump;
using Backup.Application.Dump.Models;
using Backup.Application.IO;
using Backup.Infrastructure.Dump.Abstractions.Services;
using Backup.Infrastructure.Models.Config;
using Backup.Infrastructure.Models.Config.Api;
using Backup.Infrastructure.Models.Dump;

namespace Backup.Infrastructure.Dump.Data;

internal sealed class LocalDumpDataStateCoordinator(
    AppConfig appConfig,
    IDumpLifecycleService dumpLifecycleService,
    IDataStoreGuardService dataStoreGuardService,
    IDumpPersistenceIOService dumpPersistenceIOService,
    LocalDumpDataSessionPathResolver sessionPathResolver
)
{
    private readonly AppConfig _appConfig = appConfig;
    private readonly IDumpLifecycleService _dumpLifecycleService = dumpLifecycleService;
    private readonly IDataStoreGuardService _dataStoreGuardService = dataStoreGuardService;
    private readonly IDumpPersistenceIOService _dumpPersistenceIOService = dumpPersistenceIOService;
    private readonly LocalDumpDataSessionPathResolver _sessionPathResolver = sessionPathResolver;

    public async Task<DumpData> Load(
        ApiContext context,
        CancellationToken cancellationToken = default
    )
    {
        await EnsureCreated(context, cancellationToken);

        string path = await _sessionPathResolver.GetDataPath(context, cancellationToken);

        _dataStoreGuardService.EnsureFileExists(path);

        DumpData? deserialized = await _dumpPersistenceIOService.ReadDumpData(
            path,
            cancellationToken
        );

        return _dataStoreGuardService.RequireDeserialized(deserialized, "Error in deserialize");
    }

    public async Task Save(
        ApiContext context,
        DumpData dumpData,
        CancellationToken cancellationToken = default
    )
    {
        string path = await _sessionPathResolver.GetDataPath(context, cancellationToken);
        await _dumpPersistenceIOService.WriteDumpData(path, dumpData, cancellationToken);
    }

    private async Task EnsureCreated(
        ApiContext context,
        CancellationToken cancellationToken = default
    )
    {
        string path = await _sessionPathResolver.GetDataPath(context, cancellationToken);

        if (File.Exists(path))
            return;

        DumpDataInitialization initialized = _dumpLifecycleService.CreateInitialData(
            _appConfig.Services.Dump.Count,
            context.Request.Query.Variables["count"]
        );

        DumpData dumpData = new()
        {
            Count = initialized.Count,
            QueryCount = initialized.QueryCount,
        };

        await _dumpPersistenceIOService.WriteDumpData(path, dumpData, cancellationToken);
    }
}
