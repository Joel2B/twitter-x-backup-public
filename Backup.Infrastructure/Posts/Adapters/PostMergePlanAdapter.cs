using Backup.Application.Posts;
using Backup.Infrastructure.Posts.Models;

namespace Backup.Infrastructure.Posts.Adapters;

internal sealed class PostMergePlanDecision
{
    public required string Id { get; init; }
    public required Post Merged { get; init; }
    public required Post? Current { get; init; }
    public required bool IsNew { get; init; }
    public required bool ShouldPersist { get; init; }
    public required bool ShouldLogDataChange { get; init; }
    public required bool ShouldLogIndexChange { get; init; }
}

internal static class PostMergePlanAdapter
{
    public static IReadOnlyList<PostMergePlanDecision> BuildDecisions(
        IPostMergeExecutionService postMergeExecutionService,
        string userId,
        string origin,
        IReadOnlyCollection<Post> incoming,
        IReadOnlyDictionary<string, Post> existing,
        MergeOptions options
    )
    {
        Dictionary<string, Backup.Domain.Posts.Post> existingDomain = existing.ToDictionary(
            entry => entry.Key,
            entry => PostReplicationMapper.ToDomain(entry.Value),
            StringComparer.Ordinal
        );

        List<Backup.Domain.Posts.Post> incomingDomain = incoming
            .Select(PostReplicationMapper.ToDomain)
            .ToList();

        Backup.Domain.Posts.MergeOptions domainOptions = PostReplicationMapper.ToDomain(options);

        Backup.Application.Posts.Models.PostMergeApplyPlan plan =
            postMergeExecutionService.BuildApplyPlan(
                userId,
                origin,
                incomingDomain,
                existingDomain,
                domainOptions
            );

        List<PostMergePlanDecision> decisions = new(plan.Items.Count);

        foreach (Backup.Application.Posts.Models.PostMergeApplyPlanItem item in plan.Items)
        {
            Post merged = PostReplicationMapper.ToApp(item.MergedPost);
            existing.TryGetValue(item.Id, out Post? current);

            decisions.Add(
                new PostMergePlanDecision
                {
                    Id = item.Id,
                    Merged = merged,
                    Current = current,
                    IsNew = item.IsNew,
                    ShouldPersist = item.ShouldPersist,
                    ShouldLogDataChange = item.ShouldLogDataChange,
                    ShouldLogIndexChange = item.ShouldLogIndexChange,
                }
            );
        }

        return decisions;
    }
}
