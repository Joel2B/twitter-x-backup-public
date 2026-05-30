namespace Backup.Application.Bulk;

public sealed class BulkArchiveFilePolicyService : IBulkArchiveFilePolicyService
{
    public string BuildArchivePath(string currentFilePath, DateTime now)
    {
        string directory = Path.GetDirectoryName(currentFilePath) ?? string.Empty;
        string fileName = $"{now:yyyy.MM.dd-HH.mm.ss}.json";
        return Path.Combine(directory, fileName);
    }
}
