using Backup.Infrastructure.Utility.Abstractions.Services;

namespace Backup.Infrastructure.Media.Abstractions.Services;

public interface IMediaBackupZipMutationIOService
{
    Task ReplaceEntryFromMediaStorage(
        IMediaStorage mediaStorage,
        IZipWriter zip,
        string sourcePath,
        string entryPath
    );
}
