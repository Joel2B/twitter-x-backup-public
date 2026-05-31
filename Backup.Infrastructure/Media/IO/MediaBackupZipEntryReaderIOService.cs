using Backup.Infrastructure.Media.Abstractions.Services;
using Backup.Infrastructure.Models.Utils;
using Backup.Infrastructure.Utility.Abstractions.Services;

namespace Backup.Infrastructure.Media.IO;

public sealed class MediaBackupZipEntryReaderIOService : IMediaBackupZipEntryReaderIOService
{
    public Dictionary<string, ZipEntry> ReadEntriesByFullName(IZipWriter zip) =>
        zip.GetEntries().ToDictionary(entry => entry.FullName);
}
