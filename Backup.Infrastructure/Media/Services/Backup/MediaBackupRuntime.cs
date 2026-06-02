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
    IMediaBackupData mediaBackupData,
    IDataStoreGuardService dataStoreGuardService,
    MediaBackupChunkStateRuntimeAdapter chunkStateRuntimeAdapter,
    MediaBackupChunkRecoveryCoordinator chunkRecoveryCoordinator,
    MediaBackupChunkZipCoordinator chunkZipCoordinator,
    MediaBackupChunkReportCoordinator chunkReportCoordinator,
    MediaBackupExecutionContext context
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

    public ILogger<MediaBackup> Logger { get; } = logger;
    public StorageBackup Config { get; } = config;
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
    ) => _chunkStateRuntimeAdapter.BuildStates(items);

    public void ApplyChunkEntryStates(
        Chunk chunk,
        IEnumerable<MediaBackupChunkEntryState> states
    ) => _chunkStateRuntimeAdapter.ApplyStates(chunk, states);

    public async Task<IZipWriter?> OpenChunkZipRead(Chunk chunk, string stage) =>
        await _chunkZipCoordinator.OpenChunkZipRead(this, chunk, stage);

    public async Task<IZipWriter?> OpenChunkZipWrite(Chunk chunk, string stage) =>
        await _chunkZipCoordinator.OpenChunkZipWrite(this, chunk, stage);

    public async Task RecoverCorruptChunk(Chunk chunk, string stage, Exception ex) =>
        await _chunkRecoveryCoordinator.RecoverCorruptChunk(this, chunk, stage, ex);

    public async Task RecoverApplyFailure(Chunk chunk) =>
        await _chunkRecoveryCoordinator.RecoverApplyFailure(this, chunk);

    public async Task<Dictionary<string, ZipEntry>?> ReadChunkEntries(Chunk chunk, string stage) =>
        await _chunkZipCoordinator.ReadChunkEntries(this, chunk, stage);

    public async Task<bool> MutateChunkZip(
        Chunk chunk,
        string stage,
        Func<IZipWriter, Task> mutation
    ) => await _chunkZipCoordinator.MutateChunkZip(this, chunk, stage, mutation);

    public async Task ShowInfoChunks(string? id) =>
        await _chunkReportCoordinator.ShowInfoChunks(this, id);
}
