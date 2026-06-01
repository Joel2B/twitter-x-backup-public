using Backup.Application.Posts.Models;

namespace Backup.Application.Posts;

public interface IPostHistoryLatestSelectionService
{
    PostHistoryPath? SelectLatest(IReadOnlyList<PostHistoryPath> paths);
}
