using Backup.Application.Media.Backup;
using Backup.Application.Media.Backup.Models;
using Backup.Infrastructure.Logging;
using Microsoft.Extensions.Logging;

namespace Backup.Infrastructure.Media.Services;

internal sealed class MediaBackupCalculatePhase(
    IMediaBackupCalculateExecutionService calculateExecutionService,
    MediaBackupCalculateInputBuilder mediaBackupCalculateInputBuilder,
    MediaBackupCalculateResultApplier mediaBackupCalculateResultApplier,
    MediaBackupDirectPathScanCoordinator mediaBackupDirectPathScanCoordinator
) : IMediaBackupCalculatePhase
{
    private readonly IMediaBackupCalculateExecutionService _calculateExecutionService =
        calculateExecutionService;
    private readonly MediaBackupCalculateInputBuilder _mediaBackupCalculateInputBuilder =
        mediaBackupCalculateInputBuilder;
    private readonly MediaBackupCalculateResultApplier _mediaBackupCalculateResultApplier =
        mediaBackupCalculateResultApplier;
    private readonly MediaBackupDirectPathScanCoordinator _mediaBackupDirectPathScanCoordinator =
        mediaBackupDirectPathScanCoordinator;

    public async Task Calculate(
        MediaBackupRuntime runtime,
        string? backupId,
        CancellationToken cancellationToken = default
    )
    {
        cancellationToken.ThrowIfCancellationRequested();
        await runtime.ShowInfoChunks(backupId);
        runtime.Logger.LogInfo("cloning chunks");
        MediaBackupCalculateExecutionInput input = await _mediaBackupCalculateInputBuilder.Build(
            runtime,
            cancellationToken
        );
        MediaBackupCalculateExecutionResult calculation = _calculateExecutionService.Execute(input);
        _mediaBackupCalculateResultApplier.Apply(runtime, calculation);
    }

    public async Task CalculateDirect(
        MediaBackupRuntime runtime,
        CancellationToken cancellationToken = default
    )
    {
        cancellationToken.ThrowIfCancellationRequested();
        await _mediaBackupDirectPathScanCoordinator.Scan(runtime, cancellationToken);
    }
}
