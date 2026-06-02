using Backup.Application.IO;
using Backup.Infrastructure.Core.Abstractions.Partition;
using Backup.Infrastructure.Core.Abstractions.Setup;
using Backup.Infrastructure.Dump.Abstractions.Data;
using Backup.Infrastructure.Models.Config.Data.Dump;
using Backup.Infrastructure.Models.Dump;

namespace Backup.Infrastructure.Dump.Data;

public class LocalDumpsData(
    StorageDump config,
    IPartition partition,
    IDataStoreGuardService dataStoreGuardService
) : IDumpsDataStore, ISetup
{
    public bool IsDefault { get; set; }
    private readonly LocalDumpsDataPathLayout _pathLayout = new(
        config,
        partition,
        dataStoreGuardService
    );
    private readonly LocalDumpsDataPersistenceCoordinator _persistenceCoordinator = new(
        dataStoreGuardService,
        new LocalDumpsDataPathLayout(config, partition, dataStoreGuardService)
    );

    public async Task Setup() => await _persistenceCoordinator.EnsureInitialized();

    public async Task<DumpsData> GetData(CancellationToken cancellationToken = default) =>
        await _persistenceCoordinator.Read(cancellationToken);

    public async Task Save(DumpsData dumps, CancellationToken cancellationToken = default) =>
        await _persistenceCoordinator.Write(dumps, cancellationToken);
}
