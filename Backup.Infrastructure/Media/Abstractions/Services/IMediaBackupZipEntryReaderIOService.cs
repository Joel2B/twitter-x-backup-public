using Backup.Infrastructure.Models.Utils;
using Backup.Infrastructure.Utility.Abstractions.Services;

namespace Backup.Infrastructure.Media.Abstractions.Services;

public interface IMediaBackupZipEntryReaderIOService
{
    Dictionary<string, ZipEntry> ReadEntriesByFullName(IZipWriter zip);
}
