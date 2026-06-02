using Backup.Application.Dump;
using Backup.Application.IO;
using Backup.Infrastructure.Core.Abstractions.Partition;
using Backup.Infrastructure.Dump.Abstractions.Services;
using Backup.Infrastructure.Models.Config.Data;
using Backup.Infrastructure.Models.Config.Data.Dump;

namespace Backup.Infrastructure.Dump.Data;

internal sealed class LocalDumpDataPathLayout(
    StorageDump config,
    IPartition partition,
    IDumpPathService dumpPathService,
    IDataStoreGuardService dataStoreGuardService
)
{
    private readonly StorageDump _config = config;
    private readonly IPartition _partition = partition;
    private readonly IDumpPathService _dumpPathService = dumpPathService;
    private readonly IDataStoreGuardService _dataStoreGuardService = dataStoreGuardService;

    public string GetPrimaryRootPath() => GetRootPath(_partition.GetPrimary());

    public string GetRootPath(PartitionConfig partitionConfig) =>
        _dumpPathService.BuildDumpRootPath(
            partitionConfig.Paths,
            _config.Paths.Paths,
            _config.Paths.Dumps.Paths
        );

    public string GetCurrentUserPath(string currentSession, string userId) =>
        _dumpPathService.BuildCurrentUserPath(GetPrimaryRootPath(), currentSession, userId);

    public string GetDataPath(string currentPath)
    {
        string fileName = _dataStoreGuardService.RequireConfiguredFileName(
            _config.Paths.Dumps.Dump.File
        );

        return _dumpPathService.BuildDataFilePath(currentPath, fileName);
    }

    public string GetIndexPath(string currentPath, int index) =>
        _dumpPathService.BuildIndexPath(currentPath, index);

    public string GetApiPath(string indexPath) =>
        _dumpPathService.BuildApiPath(indexPath, _config.Paths.Dumps.Dump.Api.Paths);
}
