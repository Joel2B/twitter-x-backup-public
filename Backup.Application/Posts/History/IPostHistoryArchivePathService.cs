namespace Backup.Application.Posts;

public interface IPostHistoryArchivePathService
{
    string ResolveUniqueHistoryDirectoryPath(
        string basePath,
        DateTime start,
        string directoryNameFormat,
        Func<string, bool> exists
    );
}
