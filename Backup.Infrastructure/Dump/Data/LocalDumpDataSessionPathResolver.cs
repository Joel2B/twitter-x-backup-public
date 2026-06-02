using Backup.Application.Core;
using Backup.Application.Dump;
using Backup.Application.Dump.Models;
using Backup.Infrastructure.Dump.Abstractions.Data;
using Backup.Infrastructure.Models.Config.Api;
using Backup.Infrastructure.Models.Dump;

namespace Backup.Infrastructure.Dump.Data;

internal sealed class LocalDumpDataSessionPathResolver(
    IDumpsData dumps,
    IDumpLifecycleService dumpLifecycleService,
    IDateTimeProvider dateTimeProvider,
    LocalDumpDataPathLayout pathLayout
)
{
    private readonly IDumpsData _dumps = dumps;
    private readonly IDumpLifecycleService _dumpLifecycleService = dumpLifecycleService;
    private readonly IDateTimeProvider _dateTimeProvider = dateTimeProvider;
    private readonly LocalDumpDataPathLayout _pathLayout = pathLayout;

    public async Task<string> GetCurrentPath(
        ApiContext context,
        CancellationToken cancellationToken = default
    )
    {
        DumpsData dumpsData = await _dumps.GetData(cancellationToken);
        DumpCurrentSessionResolution resolution = _dumpLifecycleService.ResolveCurrentSession(
            dumpsData.Current,
            _dateTimeProvider.Now
        );

        if (resolution.ShouldPersist)
        {
            dumpsData.Current = resolution.Current;
            await _dumps.Save(dumpsData, cancellationToken);
        }

        string path = _pathLayout.GetCurrentUserPath(resolution.Current, context.UserId);
        Directory.CreateDirectory(path);

        return path;
    }

    public async Task<string> GetDataPath(
        ApiContext context,
        CancellationToken cancellationToken = default
    )
    {
        string currentPath = await GetCurrentPath(context, cancellationToken);
        return _pathLayout.GetDataPath(currentPath);
    }

    public async Task<string> GetIndexPath(
        ApiContext context,
        int index,
        CancellationToken cancellationToken = default
    )
    {
        string currentPath = await GetCurrentPath(context, cancellationToken);
        return _pathLayout.GetIndexPath(currentPath, index);
    }

    public async Task<string> GetApiPath(
        ApiContext context,
        int index,
        CancellationToken cancellationToken = default
    )
    {
        string indexPath = await GetIndexPath(context, index, cancellationToken);
        return _pathLayout.GetApiPath(indexPath);
    }
}
