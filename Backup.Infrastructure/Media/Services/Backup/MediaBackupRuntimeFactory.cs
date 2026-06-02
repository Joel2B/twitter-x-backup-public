using Backup.Application.IO;
using Backup.Application.Media.Backup;
using Backup.Infrastructure.Media.Abstractions.Services;
using Backup.Infrastructure.Media.Abstractions.Data;
using Backup.Infrastructure.Media.Models.Backup;
using Backup.Infrastructure.Models.Config.Data.Backup;
using Backup.Infrastructure.Utility.Abstractions.Services;
using Microsoft.Extensions.Logging;

namespace Backup.Infrastructure.Media.Services;

internal sealed class MediaBackupRuntimeFactory(
    IZipWriterFactory zipWriterFactory,
    IDataStoreGuardService dataStoreGuardService,
    IMediaBackupZipEntryReaderIOService zipEntryReaderIoService,
    IMediaBackupChunkEntryStateOrchestrationService chunkEntryStateOrchestrationService,
    IMediaBackupChunkFailureApplyService chunkFailureApplyService,
    IMediaBackupChunkReportObservationAggregationService chunkReportObservationAggregationService,
    IMediaBackupChunkRuntimeCompositionService chunkRuntimeCompositionService,
    IMediaBackupChunkReportService chunkReportService
) : IMediaBackupRuntimeFactory
{
    private readonly IZipWriterFactory _zipWriterFactory = zipWriterFactory;
    private readonly IDataStoreGuardService _dataStoreGuardService = dataStoreGuardService;
    private readonly IMediaBackupZipEntryReaderIOService _zipEntryReaderIoService =
        zipEntryReaderIoService;
    private readonly IMediaBackupChunkEntryStateOrchestrationService _chunkEntryStateOrchestrationService =
        chunkEntryStateOrchestrationService;
    private readonly IMediaBackupChunkFailureApplyService _chunkFailureApplyService =
        chunkFailureApplyService;
    private readonly IMediaBackupChunkReportObservationAggregationService _chunkReportObservationAggregationService =
        chunkReportObservationAggregationService;
    private readonly IMediaBackupChunkRuntimeCompositionService _chunkRuntimeCompositionService =
        chunkRuntimeCompositionService;
    private readonly IMediaBackupChunkReportService _chunkReportService = chunkReportService;

    public MediaBackupRuntime Create(
        ILogger<MediaBackup> logger,
        StorageBackup config,
        IMediaBackupData mediaBackupData
    ) =>
        new(
            logger,
            config,
            _zipWriterFactory,
            mediaBackupData,
            _dataStoreGuardService,
            _zipEntryReaderIoService,
            _chunkEntryStateOrchestrationService,
            _chunkFailureApplyService,
            _chunkReportObservationAggregationService,
            _chunkRuntimeCompositionService,
            _chunkReportService,
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
