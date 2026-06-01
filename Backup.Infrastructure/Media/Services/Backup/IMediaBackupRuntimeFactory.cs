using Backup.Infrastructure.Media.Abstractions.Data;
using Backup.Infrastructure.Models.Config.Data.Backup;
using Microsoft.Extensions.Logging;

namespace Backup.Infrastructure.Media.Services;

internal interface IMediaBackupRuntimeFactory
{
    MediaBackupRuntime Create(
        ILogger<MediaBackup> logger,
        StorageBackup config,
        IMediaBackupData mediaBackupData
    );
}
