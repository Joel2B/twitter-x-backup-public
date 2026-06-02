using Backup.Application.Posts;
using Backup.Application.Posts.Models;

namespace Backup.Infrastructure.Posts.Data.Json;

internal sealed class LocalPostDataHashCoordinator(
    IPostHashingService postHashingService,
    IPostHashMetaParityService postHashMetaParityService,
    IPostMetaNormalizationService postMetaNormalizationService,
    IPostMetaReconciliationService postMetaReconciliationService,
    IPostMetaConsistencyValidationService postMetaConsistencyValidationService
)
{
    private readonly IPostHashingService _postHashingService = postHashingService;
    private readonly IPostHashMetaParityService _postHashMetaParityService =
        postHashMetaParityService;
    private readonly IPostMetaNormalizationService _postMetaNormalizationService =
        postMetaNormalizationService;
    private readonly IPostMetaReconciliationService _postMetaReconciliationService =
        postMetaReconciliationService;
    private readonly IPostMetaConsistencyValidationService _postMetaConsistencyValidationService =
        postMetaConsistencyValidationService;

    public void EnsureParity(int postCount, int metaCount, string storeId) =>
        _postHashMetaParityService.EnsureMatch(postCount, metaCount, storeId);

    public IReadOnlyDictionary<string, PostMetaRecord> Normalize(
        IReadOnlyList<PostMetaRecord> rows
    ) => _postMetaNormalizationService.Normalize(rows);

    public IReadOnlyDictionary<string, PostMetaRecord> Reconcile(
        IReadOnlyDictionary<string, PostMetaRecord> existing,
        IReadOnlyList<PostMetaRecord> current
    ) => _postMetaReconciliationService.Reconcile(existing, current);

    public string ComputeHash(Backup.Domain.Posts.Post domainPost) =>
        _postHashingService.Compute(domainPost);

    public void EnsureAligned(
        IEnumerable<string> postIds,
        IEnumerable<string> metaIds,
        string storeId
    ) => _postMetaConsistencyValidationService.EnsureAligned(postIds, metaIds, storeId);
}
