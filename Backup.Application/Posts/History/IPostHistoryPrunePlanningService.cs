using Backup.Application.Posts.Models;

namespace Backup.Application.Posts;

public interface IPostHistoryPrunePlanningService
{
    PostHistoryPrunePlan Plan(IReadOnlyList<PostHistoryPath> paths, int keepDays, int keepCount);
}
