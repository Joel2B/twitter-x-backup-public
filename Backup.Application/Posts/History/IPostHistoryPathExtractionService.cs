using Backup.Application.Posts.Models;

namespace Backup.Application.Posts;

public interface IPostHistoryPathExtractionService
{
    IReadOnlyList<PostHistoryPath> Extract(IEnumerable<string> paths);
}
