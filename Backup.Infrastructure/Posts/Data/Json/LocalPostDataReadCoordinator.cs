using Backup.Application.Posts;

namespace Backup.Infrastructure.Posts.Data.Json;

internal sealed class LocalPostDataReadCoordinator(
    IPostMediaInputsCompositionService postMediaInputsCompositionService,
    IPostStoreCountsAggregationService postStoreCountsAggregationService,
    IPostProfileCountAggregationService postProfileCountAggregationService,
    IPostIdentifierFilterService postIdentifierFilterService
)
{
    private readonly IPostMediaInputsCompositionService _postMediaInputsCompositionService =
        postMediaInputsCompositionService;
    private readonly IPostStoreCountsAggregationService _postStoreCountsAggregationService =
        postStoreCountsAggregationService;
    private readonly IPostProfileCountAggregationService _postProfileCountAggregationService =
        postProfileCountAggregationService;
    private readonly IPostIdentifierFilterService _postIdentifierFilterService =
        postIdentifierFilterService;

    public IReadOnlySet<string> NormalizeIdentifiers(IReadOnlyCollection<string> ids) =>
        _postIdentifierFilterService.Normalize(ids);

    public IReadOnlyList<Backup.Domain.Posts.MediaInput> ComposeMediaInputs(
        IReadOnlyList<Backup.Domain.Posts.Post> domainPosts
    ) => _postMediaInputsCompositionService.Compose(domainPosts);

    public Backup.Domain.Posts.PostStoreCounts ComputeStoreCounts(
        IReadOnlyList<Backup.Domain.Posts.Post> domainPosts,
        int hashMetaCount
    ) => _postStoreCountsAggregationService.Compute(domainPosts, hashMetaCount);

    public IReadOnlyDictionary<string, int> CountByProfileIds(
        IEnumerable<string> profileIds,
        IReadOnlySet<string> filter
    ) => _postProfileCountAggregationService.CountByProfileIds(profileIds, filter);
}
