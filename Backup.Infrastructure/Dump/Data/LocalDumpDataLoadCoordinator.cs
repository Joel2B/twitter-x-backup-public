using Backup.Application.Dump;
using Backup.Infrastructure.Dump.Abstractions.Data;
using Backup.Infrastructure.Models.Config.Api;
using Backup.Infrastructure.Models.Dump;

namespace Backup.Infrastructure.Dump.Data;

internal sealed class LocalDumpDataLoadCoordinator(
    IDumpsData dumps,
    IDumpContextEligibilityService dumpContextEligibilityService,
    IDumpLifecycleService dumpLifecycleService,
    LocalDumpDataStateCoordinator stateCoordinator
)
{
    private readonly IDumpsData _dumps = dumps;
    private readonly IDumpContextEligibilityService _dumpContextEligibilityService =
        dumpContextEligibilityService;
    private readonly IDumpLifecycleService _dumpLifecycleService = dumpLifecycleService;
    private readonly LocalDumpDataStateCoordinator _stateCoordinator = stateCoordinator;

    public async Task<DumpData?> Load(
        ApiContext context,
        CancellationToken cancellationToken = default
    )
    {
        if (!_dumpContextEligibilityService.ShouldLoadDumpData(context.Count))
            return null;

        DumpsData dumpsData = await _dumps.GetData(cancellationToken);
        DumpData dumpData = await _stateCoordinator.Load(context, cancellationToken);
        dumpData.Type = _dumpLifecycleService.ResolveType(
            dumpsData.Current,
            context.Id,
            dumpData.Type
        );
        return dumpData;
    }
}
