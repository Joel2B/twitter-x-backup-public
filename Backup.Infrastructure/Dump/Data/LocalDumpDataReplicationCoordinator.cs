using Backup.Application.Core;
using Backup.Application.Dump;
using Backup.Application.Dump.Models;
using Backup.Infrastructure.Core.Abstractions.Partition;
using Backup.Infrastructure.Dump.Abstractions.Services;
using Backup.Infrastructure.Models.Config.Api;
using Backup.Infrastructure.Models.Config.Data;

namespace Backup.Infrastructure.Dump.Data;

internal sealed class LocalDumpDataReplicationCoordinator(
    ISecondaryStoreSelectionService secondaryStoreSelectionService,
    IPartition partition,
    IDumpReplicationPlanningService dumpReplicationPlanningService,
    IDumpPersistenceIOService dumpPersistenceIOService,
    LocalDumpDataPathLayout pathLayout,
    LocalDumpDataSessionPathResolver sessionPathResolver
)
{
    private readonly ISecondaryStoreSelectionService _secondaryStoreSelectionService =
        secondaryStoreSelectionService;
    private readonly IPartition _partition = partition;
    private readonly IDumpReplicationPlanningService _dumpReplicationPlanningService =
        dumpReplicationPlanningService;
    private readonly IDumpPersistenceIOService _dumpPersistenceIOService = dumpPersistenceIOService;
    private readonly LocalDumpDataPathLayout _pathLayout = pathLayout;
    private readonly LocalDumpDataSessionPathResolver _sessionPathResolver = sessionPathResolver;

    public async Task Replicate(ApiContext context, CancellationToken cancellationToken = default)
    {
        PartitionConfig primary = _partition.GetPrimary();
        IReadOnlyList<PartitionConfig> partitions =
            _secondaryStoreSelectionService.SelectSecondaries(_partition.GetPartitions(), primary);

        string mainPath = await _sessionPathResolver.GetCurrentPath(context, cancellationToken);
        string primaryPath = _pathLayout.GetRootPath(primary);
        DumpReplicationPlan plan = _dumpReplicationPlanningService.Plan(
            primaryPath,
            mainPath,
            partitions.Select(_pathLayout.GetRootPath)
        );

        foreach (string path in plan.TargetPaths)
        {
            cancellationToken.ThrowIfCancellationRequested();
            _dumpPersistenceIOService.CopyDirectory(mainPath, path);
        }
    }
}
