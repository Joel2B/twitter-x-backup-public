using Backup.Application.Posts.Models;

namespace Backup.Application.Posts;

public sealed class PostHistoryLatestSelectionService : IPostHistoryLatestSelectionService
{
    public PostHistoryPath? SelectLatest(IReadOnlyList<PostHistoryPath> paths) =>
        paths.OrderByDescending(path => path.Date).FirstOrDefault();
}
