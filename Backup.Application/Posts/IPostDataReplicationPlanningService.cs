using Backup.Application.Posts.Models;

namespace Backup.Application.Posts;

public interface IPostDataReplicationPlanningService
{
    IReadOnlyList<PostDataFileReplicationOperation> Plan(
        IReadOnlyList<string> sourcePaths,
        IReadOnlyList<string> targetPaths
    );
}
