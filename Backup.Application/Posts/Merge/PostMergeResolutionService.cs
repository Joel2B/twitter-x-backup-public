using Backup.Application.Posts.Models;
using Backup.Domain.Posts;

namespace Backup.Application.Posts;

public sealed class PostMergeResolutionService(IPostMergeService postMergeService)
    : IPostMergeResolutionService
{
    private readonly IPostMergeService _postMergeService = postMergeService;

    public IReadOnlyList<PostMergeResolutionItem> Resolve(
        string userId,
        string origin,
        IReadOnlyCollection<Post> incoming,
        IReadOnlyDictionary<string, Post> existing,
        MergeOptions options
    )
    {
        List<Post> normalizedIncoming = incoming
            .Where(post => !string.IsNullOrWhiteSpace(post.Id))
            .GroupBy(post => post.Id, StringComparer.Ordinal)
            .Select(group => group.Last().Clone())
            .ToList();

        List<PostMergeResolutionItem> resolved = [];

        foreach (Post incomingPost in normalizedIncoming)
        {
            if (!existing.TryGetValue(incomingPost.Id, out Post? current))
            {
                resolved.Add(
                    new PostMergeResolutionItem
                    {
                        Id = incomingPost.Id,
                        MergedPost = incomingPost.Clone(),
                        IsNew = true,
                        HasDataChange = false,
                        HasIndexChange = false,
                    }
                );

                continue;
            }

            PostMergeOutcome merge = _postMergeService.Merge(
                userId,
                origin,
                current,
                incomingPost,
                options
            );

            resolved.Add(
                new PostMergeResolutionItem
                {
                    Id = incomingPost.Id,
                    MergedPost = merge.MergedPost.Clone(),
                    IsNew = false,
                    HasDataChange = merge.HasDataChange,
                    HasIndexChange = merge.HasIndexChange,
                }
            );
        }

        return resolved;
    }
}
