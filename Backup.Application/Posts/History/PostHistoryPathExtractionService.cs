using Backup.Application.IO;
using Backup.Application.Posts.Models;

namespace Backup.Application.Posts;

public sealed class PostHistoryPathExtractionService : IPostHistoryPathExtractionService
{
    public IReadOnlyList<PostHistoryPath> Extract(IEnumerable<string> paths)
    {
        List<PostHistoryPath> result = [];

        foreach (string path in paths)
        {
            DateTime? date = PathFormattingPolicy.ParseTimestampFromPath(path, isDirectory: true);
            if (date is null)
                continue;

            result.Add(new PostHistoryPath(path, date.Value));
        }

        return result;
    }
}
