namespace Backup.Application.Bulk;

public interface IBulkArchiveFilePolicyService
{
    string BuildArchivePath(string currentFilePath, DateTime now);
}
