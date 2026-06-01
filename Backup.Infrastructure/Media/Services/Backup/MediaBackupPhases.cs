using Backup.Infrastructure.Media.Models.Backup;

namespace Backup.Infrastructure.Media.Services;

internal interface IMediaBackupCalculatePhase
{
    Task Calculate(
        MediaBackupRuntime runtime,
        string? backupId,
        CancellationToken cancellationToken = default
    );
    Task CalculateDirect(MediaBackupRuntime runtime, CancellationToken cancellationToken = default);
}

internal interface IMediaBackupApplyPhase
{
    Task Apply(
        MediaBackupRuntime runtime,
        string? backupId,
        CancellationToken cancellationToken = default
    );
    Task ApplyDirect(MediaBackupRuntime runtime, CancellationToken cancellationToken = default);
}

internal interface IMediaBackupDuplicatePhase
{
    Task CheckDuplicates(MediaBackupRuntime runtime, CancellationToken cancellationToken = default);
}

internal interface IMediaBackupMetadataPhase
{
    Task SetFileSizes(MediaBackupRuntime runtime, CancellationToken cancellationToken = default);
}

internal interface IMediaBackupIntegrityPhase
{
    Task CheckIntegrity(MediaBackupRuntime runtime, CancellationToken cancellationToken = default);
    Task FixIntegrity(MediaBackupRuntime runtime, CancellationToken cancellationToken = default);
}
