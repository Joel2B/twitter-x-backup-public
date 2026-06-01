using Backup.Infrastructure.Media.Models.Backup;

namespace Backup.Infrastructure.Media.Services;

internal interface IMediaBackupCalculatePhase
{
    Task Calculate(MediaBackupRuntime runtime, string? backupId);
    Task CalculateDirect(MediaBackupRuntime runtime);
}

internal interface IMediaBackupApplyPhase
{
    Task Apply(MediaBackupRuntime runtime, string? backupId);
    Task ApplyDirect(MediaBackupRuntime runtime);
}

internal interface IMediaBackupDuplicatePhase
{
    Task CheckDuplicates(MediaBackupRuntime runtime);
}

internal interface IMediaBackupMetadataPhase
{
    Task SetFileSizes(MediaBackupRuntime runtime);
}

internal interface IMediaBackupIntegrityPhase
{
    Task CheckIntegrity(MediaBackupRuntime runtime);
    Task FixIntegrity(MediaBackupRuntime runtime);
}
