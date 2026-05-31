using Backup.Infrastructure.Media.Abstractions.Services;
using Backup.Infrastructure.Utility.Abstractions.Services;

namespace Backup.Infrastructure.Media.IO;

public sealed class MediaBackupZipMutationIOService : IMediaBackupZipMutationIOService
{
    public async Task ReplaceEntryFromMediaStorage(
        IMediaStorage mediaStorage,
        IZipWriter zip,
        string sourcePath,
        string entryPath
    )
    {
        await using Stream read = await mediaStorage.Read(sourcePath);
        zip.RemoveEntry(entryPath);
        await zip.AddEntry(entryPath, read);
    }
}
