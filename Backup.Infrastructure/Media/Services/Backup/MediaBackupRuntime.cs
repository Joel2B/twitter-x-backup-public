using System.Collections.Concurrent;
using Backup.Application.IO;
using Backup.Application.Media.Backup;
using Backup.Application.Media.Backup.Models;
using Backup.Infrastructure.Logging;
using Backup.Infrastructure.Media.Abstractions.Data;
using Backup.Infrastructure.Media.Abstractions.Services;
using Backup.Infrastructure.Media.Models;
using Backup.Infrastructure.Media.Models.Backup;
using Backup.Infrastructure.Models.Config.Data.Backup;
using Backup.Infrastructure.Models.Utils;
using Backup.Infrastructure.Utility.Abstractions.Services;
using Microsoft.Extensions.Logging;

namespace Backup.Infrastructure.Media.Services;

internal sealed class MediaBackupRuntime(
    ILogger<MediaBackup> logger,
    StorageBackup config,
    IZipWriterFactory zipWriterFactory,
    IMediaBackupData mediaBackupData,
    IDataStoreGuardService dataStoreGuardService,
    IMediaBackupChunkEntryStateOrchestrationService chunkEntryStateOrchestrationService,
    IMediaBackupChunkFailureApplyService chunkFailureApplyService,
    IMediaBackupChunkReportObservationAggregationService chunkReportObservationAggregationService,
    IMediaBackupChunkRuntimeCompositionService chunkRuntimeCompositionService,
    IMediaBackupChunkReportService chunkReportService,
    MediaBackupExecutionContext context
)
{
    private readonly IDataStoreGuardService _dataStoreGuardService = dataStoreGuardService;
    private readonly IMediaBackupChunkEntryStateOrchestrationService _chunkEntryStateOrchestrationService =
        chunkEntryStateOrchestrationService;
    private readonly IMediaBackupChunkFailureApplyService _chunkFailureApplyService =
        chunkFailureApplyService;
    private readonly IMediaBackupChunkReportObservationAggregationService _chunkReportObservationAggregationService =
        chunkReportObservationAggregationService;
    private readonly IMediaBackupChunkRuntimeCompositionService _chunkRuntimeCompositionService =
        chunkRuntimeCompositionService;
    private readonly IMediaBackupChunkReportService _chunkReportService = chunkReportService;

    public ILogger<MediaBackup> Logger { get; } = logger;
    public StorageBackup Config { get; } = config;
    public IZipWriterFactory ZipWriterFactory { get; } = zipWriterFactory;
    public IMediaBackupData MediaBackupData { get; } = mediaBackupData;
    public MediaBackupExecutionContext Context { get; } = context;
    public bool Stop { get; } = false;

    public IMediaStorage MediaData =>
        _dataStoreGuardService.RequireInitialized(Context.MediaData, "media data not initialized");

    public int GetDuplicateCleanupPreviewLimit() =>
        Config.Chunk.Path.DuplicateCleanupPreviewLimit > 0
            ? Config.Chunk.Path.DuplicateCleanupPreviewLimit
            : 10;

    public IReadOnlyList<MediaBackupChunkEntryState> BuildChunkEntryStates(
        IEnumerable<ChunkData> items
    ) => _chunkEntryStateOrchestrationService.BuildStates(items.Select(ToEntryRecord));

    public void ApplyChunkEntryStates(Chunk chunk, IEnumerable<MediaBackupChunkEntryState> states)
    {
        IReadOnlyList<MediaBackupChunkEntryRecord> updated =
            _chunkEntryStateOrchestrationService.ApplyStates(
                chunk.Data.Select(ToEntryRecord),
                states
            );

        Dictionary<string, MediaBackupChunkEntryRecord> byPath = updated.ToDictionary(
            item => item.Path,
            StringComparer.Ordinal
        );

        foreach (ChunkData data in chunk.Data)
        {
            if (!byPath.TryGetValue(data.Path, out MediaBackupChunkEntryRecord? state))
                continue;

            data.Hash = state.Hash;
            data.FileSize = state.FileSize;
            data.Crc32 = state.Crc32;
        }
    }

    public async Task<IZipWriter?> OpenChunkZipRead(Chunk chunk, string stage)
    {
        Stream? zipFile = null;

        try
        {
            zipFile = await MediaBackupData.GetChunk(chunk);

            if (zipFile is null)
            {
                Logger.LogWarning(
                    "chunk {chunk} zip missing while reading ({stage})",
                    chunk.Id,
                    stage
                );
                return null;
            }

            return ZipWriterFactory.Open(zipFile);
        }
        catch (Exception ex)
        {
            zipFile?.Dispose();
            await RecoverCorruptChunk(chunk, stage, ex);
            return null;
        }
    }

    public async Task<IZipWriter?> OpenChunkZipWrite(Chunk chunk, string stage)
    {
        Stream? zipFile = null;

        try
        {
            zipFile = await MediaBackupData.GetChunk(chunk);

            if (zipFile is null)
            {
                Logger.LogError(
                    "chunk {chunk} zip stream unavailable for write ({stage})",
                    chunk.Id,
                    stage
                );
                return null;
            }

            return ZipWriterFactory.Create(zipFile);
        }
        catch (Exception ex)
        {
            zipFile?.Dispose();
            await RecoverCorruptChunk(chunk, stage, ex);
            return null;
        }
    }

    public async Task RecoverCorruptChunk(Chunk chunk, string stage, Exception ex)
    {
        Logger.LogError(
            ex,
            "chunk {chunk} zip failed ({stage}); deleting and scheduling rebuild",
            chunk.Id,
            stage
        );

        await MediaBackupData.DeleteChunk(chunk);

        IReadOnlyList<MediaBackupChunkEntryState> resetStates =
            _chunkFailureApplyService.ApplyForCorruptChunk(BuildChunkEntryStates(chunk.Data));
        ApplyChunkEntryStates(chunk, resetStates);

        await MediaBackupData.Save([chunk]);
    }

    public async Task ShowInfoChunks(string? id)
    {
        Logger.LogInfo("{id,-3} {paths,-10} {size}", "id", "paths", "size (GiB)");
        List<MediaBackupChunkReportEntryInput> reportEntries = [];

        foreach (KeyValuePair<int, Chunk> kvp in Context.Chunks)
        {
            if (kvp.Value.Data.Count == 0)
                continue;

            foreach (ChunkData chunkData in Context.Chunks[kvp.Key].Data)
            {
                MediaCacheEntry? cache = await MediaData.GetCache(chunkData.Path);

                reportEntries.Add(
                    new MediaBackupChunkReportEntryInput
                    {
                        ChunkId = kvp.Key,
                        PathCount = kvp.Value.Data.Count,
                        FileSizeBytes = cache?.Size?.File ?? 0,
                    }
                );
            }
        }

        IReadOnlyList<MediaBackupChunkReportObservationInput> observationInputs =
            _chunkReportObservationAggregationService.Aggregate(reportEntries);

        IReadOnlyList<MediaBackupChunkReportObservation> observations =
            _chunkRuntimeCompositionService.BuildChunkReportObservations(observationInputs);

        IReadOnlyList<MediaBackupChunkReportRow> rows = _chunkReportService.Build(observations);

        foreach (MediaBackupChunkReportRow row in rows)
        {
            Logger.LogInformation(
                "{id,-3} {paths,-10} {size}",
                row.ChunkId,
                row.PathCount,
                row.SizeGiB
            );
        }
    }

    private static MediaBackupChunkEntryRecord ToEntryRecord(ChunkData item) =>
        new()
        {
            Path = item.Path,
            Hash = item.Hash,
            FileSize = item.FileSize,
            Crc32 = item.Crc32,
        };
}
