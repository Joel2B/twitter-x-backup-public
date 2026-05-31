using Backup.Domain.Posts;

namespace Backup.Application.Posts;

public sealed class PostStoreCountsAggregationService(
    IPostChangeComputationService postChangeComputationService
) : IPostStoreCountsAggregationService
{
    private readonly IPostChangeComputationService _postChangeComputationService =
        postChangeComputationService;

    public PostStoreCounts Compute(IReadOnlyCollection<Post> posts, int hashMetaCount)
    {
        if (posts.Count == 0)
        {
            return new PostStoreCounts
            {
                Posts = 0,
                Profiles = 0,
                Hashtags = 0,
                Medias = 0,
                MediaVariants = 0,
                IndexEntries = 0,
                Changes = 0,
                ChangeFields = 0,
                HashMeta = hashMetaCount,
            };
        }

        int hashtags = 0;
        int medias = 0;
        int mediaVariants = 0;
        int indexEntries = 0;
        int changes = 0;
        int changeFields = 0;

        foreach (Post post in posts)
        {
            hashtags += post.Hashtags?.Count ?? 0;

            if (post.Medias is not null)
            {
                medias += post.Medias.Count;
                mediaVariants += post.Medias.Sum(media => media.VideoInfo?.Variants?.Count ?? 0);
            }

            indexEntries += post.Index.Sum(userIndex => userIndex.Value.Count);

            if (post.Changes.Count == 0)
                continue;

            IReadOnlyList<Models.PostComputedChange> computedChanges = _postChangeComputationService
                .Compute(post);

            changes += computedChanges.Count;
            changeFields += computedChanges.Sum(change => change.Fields.Count);
        }

        return new PostStoreCounts
        {
            Posts = posts.Count,
            Profiles = posts.Select(post => post.Profile.Id).Distinct(StringComparer.Ordinal).Count(),
            Hashtags = hashtags,
            Medias = medias,
            MediaVariants = mediaVariants,
            IndexEntries = indexEntries,
            Changes = changes,
            ChangeFields = changeFields,
            HashMeta = hashMetaCount,
        };
    }
}
