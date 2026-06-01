namespace Backup.Application.Posts;

public sealed class PostHistoryArchivePathService : IPostHistoryArchivePathService
{
    public string ResolveUniqueHistoryDirectoryPath(
        string basePath,
        DateTime start,
        string directoryNameFormat,
        Func<string, bool> exists
    )
    {
        DateTime candidate = start;

        while (true)
        {
            string path = Path.Combine(basePath, candidate.ToString(directoryNameFormat));

            if (!exists(path))
                return path;

            candidate = candidate.AddSeconds(1);
        }
    }
}
