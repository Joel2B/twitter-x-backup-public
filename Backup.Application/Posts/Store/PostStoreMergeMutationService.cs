using Backup.Application.Posts.Models;
using Backup.Domain.Posts;

namespace Backup.Application.Posts;

public sealed class PostStoreMergeMutationService(
    IPostMergeExecutionService postMergeExecutionService,
    IPostHashingService postHashingService
) : IPostStoreMergeMutationService
{
    private readonly IPostMergeExecutionService _postMergeExecutionService =
        postMergeExecutionService;
    private readonly IPostHashingService _postHashingService = postHashingService;

    public IReadOnlyList<PostStoreMergeMutation> BuildMergeMutations(
        string userId,
        string origin,
        IReadOnlyList<Post> incoming,
        IReadOnlyDictionary<string, Post> existing,
        MergeOptions options
    )
    {
        PostMergeApplyPlan plan = _postMergeExecutionService.BuildApplyPlan(
            userId,
            origin,
            incoming,
            existing,
            options
        );

        List<PostStoreMergeMutation> mutations = new(plan.Items.Count);

        foreach (PostMergeApplyPlanItem item in plan.Items)
        {
            mutations.Add(
                new PostStoreMergeMutation
                {
                    Id = item.Id,
                    MergedPost = item.MergedPost,
                    IsNew = item.IsNew,
                    ShouldPersist = item.ShouldPersist,
                    ShouldLogDataChange = item.ShouldLogDataChange,
                    ShouldLogIndexChange = item.ShouldLogIndexChange,
                    Hash = _postHashingService.Compute(item.MergedPost),
                    Deleted = item.MergedPost.Deleted,
                }
            );
        }

        return mutations;
    }
}
