namespace Backup.Application.Media;

public interface IMediaLogFilePolicyService
{
    string CreateFileName(DateTime now);
    string? SelectLatestFilePath(IEnumerable<string> paths);
}
