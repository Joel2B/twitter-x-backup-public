using Backup.Application.IO;
using Backup.Application.Media.Backup;
using Backup.Infrastructure.Media.Abstractions.Data;
using Backup.Infrastructure.Media.Abstractions.Services;
using Backup.Infrastructure.Media.Models.Backup;
using Backup.Infrastructure.Models.Config.Data.Backup;
using Microsoft.Extensions.Logging;

namespace Backup.Infrastructure.Media.Services;

internal sealed class MediaBackupRuntimeFactory(
    IDataStoreGuardService dataStoreGuardService,
    MediaBackupChunkStateRuntimeAdapter chunkStateRuntimeAdapter,
    MediaBackupChunkRecoveryCoordinator chunkRecoveryCoordinator,
    MediaBackupChunkZipCoordinator chunkZipCoordinator,
    MediaBackupChunkReportCoordinator chunkReportCoordinator
)
{
    private readonly IDataStoreGuardService _dataStoreGuardService = dataStoreGuardService;
    private readonly MediaBackupChunkStateRuntimeAdapter _chunkStateRuntimeAdapter =
        chunkStateRuntimeAdapter;
    private readonly MediaBackupChunkRecoveryCoordinator _chunkRecoveryCoordinator =
        chunkRecoveryCoordinator;
    private readonly MediaBackupChunkZipCoordinator _chunkZipCoordinator = chunkZipCoordinator;
    private readonly MediaBackupChunkReportCoordinator _chunkReportCoordinator =
        chunkReportCoordinator;

    public MediaBackupRuntime Create(
        ILogger<MediaBackup> logger,
        StorageBackup config,
        IMediaBackupData mediaBackupData
    ) =>
        new(
            logger,
            config,
            mediaBackupData,
            _dataStoreGuardService,
            _chunkStateRuntimeAdapter,
            _chunkRecoveryCoordinator,
            _chunkZipCoordinator,
            _chunkReportCoordinator,
            new MediaBackupExecutionContext(
                new BackupChunks
                {
                    Chunks = new()
                    {
                        Total = config.Chunk.Count,
                        Path = new() { Increase = config.Chunk.Path.Increase },
                    },
                }
            )
        );
}
