using Backup.Application.Posts.Models;

namespace Backup.Application.Posts;

public sealed class PostDataReplicationPlanningService : IPostDataReplicationPlanningService
{
    public IReadOnlyList<PostDataFileReplicationOperation> Plan(
        IReadOnlyList<string> sourcePaths,
        IReadOnlyList<string> targetPaths
    )
    {
        if (sourcePaths.Count != targetPaths.Count)
            throw new InvalidOperationException(
                $"Source/target path count mismatch: {sourcePaths.Count}/{targetPaths.Count}"
            );

        List<PostDataFileReplicationOperation> operations = new(sourcePaths.Count);

        for (int i = 0; i < sourcePaths.Count; i++)
        {
            operations.Add(
                new PostDataFileReplicationOperation
                {
                    SourcePath = sourcePaths[i],
                    TargetPath = targetPaths[i],
                }
            );
        }

        return operations;
    }
}
