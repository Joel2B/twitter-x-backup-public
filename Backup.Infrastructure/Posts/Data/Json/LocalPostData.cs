using Backup.Application.Core;
using Backup.Application.IO;
using Backup.Application.Posts;
using Backup.Infrastructure.Core.Abstractions.Partition;
using Backup.Infrastructure.Core.Abstractions.Setup;
using Backup.Infrastructure.Models.Config;
using Backup.Infrastructure.Models.Config.Data;
using Backup.Infrastructure.Models.Config.Data.Posts;
using Backup.Infrastructure.Models.Data.Json;
using Backup.Infrastructure.Posts.Abstractions.Data;
using Backup.Infrastructure.Posts.Adapters;
using Backup.Infrastructure.Posts.Models;
using Microsoft.Extensions.Logging;

namespace Backup.Infrastructure.Posts.Data.Json;

// Facade for JSON-backed post storage. Public behavior should remain stable;
// internal responsibilities are intentionally delegated to collaborators.
public partial class LocalPostData(
    ILogger<LocalPostData> _logger,
    AppConfig _appConfig,
    StoragePost _config,
    IPartition _partition,
    IPostStoreMergeMutationService postStoreMergeMutationService,
    IPostSoftDeleteExecutionService postSoftDeleteExecutionService,
    IPostSnapshotNormalizationService postSnapshotNormalizationService,
    IPostMediaInputsCompositionService postMediaInputsCompositionService,
    IPostHashingService postHashingService,
    IPostHashMetaParityService postHashMetaParityService,
    IPostMetaNormalizationService postMetaNormalizationService,
    IPostMetaReconciliationService postMetaReconciliationService,
    IPostHistoryPathExtractionService postHistoryPathExtractionService,
    IPostHistoryPrunePlanningService postHistoryPrunePlanningService,
    IPostSnapshotVerificationExecutionService postSnapshotVerificationExecutionService,
    IPostDataReplicationPlanningService postDataReplicationPlanningService,
    IPostChangeComputationService postChangeComputationService,
    IPostChangeReadModelProjectionService postChangeReadModelProjectionService,
    IPostStoreCountsAggregationService postStoreCountsAggregationService,
    IPostProfileCountAggregationService postProfileCountAggregationService,
    IPostMetaConsistencyValidationService postMetaConsistencyValidationService,
    IPostTableProjectionService postTableProjectionService,
    IPostTableMaterializationService postTableMaterializationService,
    IPostIdentifierFilterService postIdentifierFilterService,
    IDataStoreGuardService dataStoreGuardService,
    IPostHistoryArchivePathService postHistoryArchivePathService,
    IDateTimeProvider dateTimeProvider
) : IPostDataStore, ISetup
{
    public string? Id { get; set; }
    public bool IsDefault { get; set; }

    private readonly ILogger<LocalPostData> _logger = _logger;
    private readonly AppConfig _appConfig = _appConfig;
    private readonly StoragePost _config = _config;
    private readonly IPartition _partition = _partition;
    private readonly IPostStoreMergeMutationService _postStoreMergeMutationService =
        postStoreMergeMutationService;
    private readonly IPostSoftDeleteExecutionService _postSoftDeleteExecutionService =
        postSoftDeleteExecutionService;
    private readonly IPostSnapshotNormalizationService _postSnapshotNormalizationService =
        postSnapshotNormalizationService;
    private readonly IPostMediaInputsCompositionService _postMediaInputsCompositionService =
        postMediaInputsCompositionService;
    private readonly IPostHashingService _postHashingService = postHashingService;
    private readonly IPostHashMetaParityService _postHashMetaParityService =
        postHashMetaParityService;
    private readonly IPostMetaNormalizationService _postMetaNormalizationService =
        postMetaNormalizationService;
    private readonly IPostMetaReconciliationService _postMetaReconciliationService =
        postMetaReconciliationService;
    private readonly IPostHistoryPathExtractionService _postHistoryPathExtractionService =
        postHistoryPathExtractionService;
    private readonly IPostHistoryPrunePlanningService _postHistoryPrunePlanningService =
        postHistoryPrunePlanningService;
    private readonly IPostSnapshotVerificationExecutionService _postSnapshotVerificationExecutionService =
        postSnapshotVerificationExecutionService;
    private readonly IPostDataReplicationPlanningService _postDataReplicationPlanningService =
        postDataReplicationPlanningService;
    private readonly IPostChangeComputationService _postChangeComputationService =
        postChangeComputationService;
    private readonly IPostChangeReadModelProjectionService _postChangeReadModelProjectionService =
        postChangeReadModelProjectionService;
    private readonly IPostStoreCountsAggregationService _postStoreCountsAggregationService =
        postStoreCountsAggregationService;
    private readonly IPostProfileCountAggregationService _postProfileCountAggregationService =
        postProfileCountAggregationService;
    private readonly IPostMetaConsistencyValidationService _postMetaConsistencyValidationService =
        postMetaConsistencyValidationService;
    private readonly IPostTableProjectionService _postTableProjectionService =
        postTableProjectionService;
    private readonly IPostTableMaterializationService _postTableMaterializationService =
        postTableMaterializationService;
    private readonly IPostIdentifierFilterService _postIdentifierFilterService =
        postIdentifierFilterService;
    private readonly IDataStoreGuardService _dataStoreGuardService = dataStoreGuardService;
    private readonly IPostHistoryArchivePathService _postHistoryArchivePathService =
        postHistoryArchivePathService;
    private readonly IDateTimeProvider _dateTimeProvider = dateTimeProvider;

    private Dictionary<string, Post>? _postsCache = null;
    private Dictionary<string, PostMetaRow>? _postMetaCache = null;

    public Task Setup()
    {
        SetupDirectory();

        return Task.CompletedTask;
    }

    public async Task<List<Post>?> GetAll()
    {
        Dictionary<string, Post>? posts = await GetCache();
        return posts is null ? null : [.. posts.Values];
    }

    public async Task<Dictionary<string, string>> GetHashesById()
    {
        Dictionary<string, Post>? posts = await GetCache();
        Dictionary<string, PostMetaRow> postMeta = await GetPostMetaCache();

        int postCount = posts?.Count ?? 0;
        int metaCount = postMeta.Count;

        _postHashMetaParityService.EnsureMatch(postCount, metaCount, Id ?? _config.Id ?? "unknown");

        return postMeta.ToDictionary(
            entry => entry.Key,
            entry => entry.Value.Hash,
            StringComparer.Ordinal
        );
    }

    public async Task<List<Post>> GetByIds(IReadOnlyCollection<string> ids)
    {
        IReadOnlySet<string> filter = _postIdentifierFilterService.Normalize(ids);

        if (filter.Count == 0)
            return [];

        Dictionary<string, Post>? posts = await GetCache();

        if (posts is null)
            return [];

        List<Post> result = new(filter.Count);

        foreach (string id in filter)
        {
            if (!posts.TryGetValue(id, out Post? post))
                continue;

            result.Add(post.Clone());
        }

        return result;
    }

    public async Task<int> GetCount() => (await GetCache())?.Count ?? 0;

    public async Task<PostStoreCounts> GetStoreCounts()
    {
        Dictionary<string, Post>? cache = await GetCache();
        List<Backup.Domain.Posts.Post> domainPosts = cache is null
            ? []
            : cache.Values.Select(PostReplicationMapper.ToDomain).ToList();

        Backup.Domain.Posts.PostStoreCounts counts = _postStoreCountsAggregationService.Compute(
            domainPosts,
            _postMetaCache?.Count ?? cache?.Count ?? 0
        );

        return new PostStoreCounts
        {
            Posts = counts.Posts,
            Profiles = counts.Profiles,
            Hashtags = counts.Hashtags,
            Medias = counts.Medias,
            MediaVariants = counts.MediaVariants,
            IndexEntries = counts.IndexEntries,
            Changes = counts.Changes,
            ChangeFields = counts.ChangeFields,
            HashMeta = counts.HashMeta,
        };
    }

    public async Task<int> MarkDeletedExcept(
        string userId,
        string origin,
        IReadOnlyCollection<string> keepPostIds
    )
    {
        await GetCache();

        if (_postsCache is null)
            return 0;

        List<Backup.Domain.Posts.Post> domainPosts = _postsCache
            .Values.Select(PostReplicationMapper.ToDomain)
            .ToList();
        IReadOnlyDictionary<string, bool> deletedById = domainPosts.ToDictionary(
            post => post.Id,
            post => post.Deleted,
            StringComparer.Ordinal
        );

        IReadOnlySet<string> idsToDelete = _postSoftDeleteExecutionService.SelectIdsToMarkDeleted(
            userId,
            origin,
            keepPostIds,
            domainPosts,
            deletedById
        );

        if (idsToDelete.Count == 0)
            return 0;

        List<Post> deletedPosts = idsToDelete
            .Select(id =>
            {
                if (!_postsCache.TryGetValue(id, out Post? existing))
                    return null;

                Post deletedPost = existing.Clone();
                deletedPost.Deleted = true;
                return deletedPost;
            })
            .Where(post => post is not null)
            .Select(post => post!)
            .ToList();

        if (deletedPosts.Count == 0)
            return 0;

        await AddPosts(userId, origin, deletedPosts, new() { Index = false });
        return deletedPosts.Count;
    }

    public async Task<List<MediaInput>?> GetMediaInputs()
    {
        Dictionary<string, Post>? posts = await GetCache();
        if (posts is null)
            return null;

        List<Backup.Domain.Posts.Post> domainPosts = posts
            .Values.Select(PostReplicationMapper.ToDomain)
            .ToList();

        IReadOnlyList<Backup.Domain.Posts.MediaInput> composed =
            _postMediaInputsCompositionService.Compose(domainPosts);

        return composed.Select(PostReplicationMapper.ToApp).ToList();
    }

    public async Task<Dictionary<string, int>> GetPostCountsByProfileIds(
        IReadOnlyCollection<string> profileIds
    )
    {
        IReadOnlySet<string> filter = _postIdentifierFilterService.Normalize(profileIds);

        if (filter.Count == 0)
            return [];

        Dictionary<string, Post>? cache = await GetCache();
        if (cache is null)
            return [];

        IReadOnlyDictionary<string, int> counts =
            _postProfileCountAggregationService.CountByProfileIds(
                cache.Values.Select(post => post.Profile.Id),
                filter
            );

        return counts.ToDictionary(
            entry => entry.Key,
            entry => entry.Value,
            StringComparer.Ordinal
        );
    }

    public async Task Save()
    {
        if (_postsCache is null)
            return;

        List<Post> posts = [.. _postsCache.Values];
        LocalPostTables tables = BuildTables(posts);
        Dictionary<string, PostMetaRow> postMeta = await EnsurePostMetaCache(posts);

        await SaveTables(tables, postMeta);
        Replicate();

        SetCache(posts);
        _postMetaCache = postMeta;
    }

    public async Task Prune()
    {
        _logger.LogInformation("running prune");
        _logger.LogInformation("prune: {value}", _config.Tasks.Prune);

        if (!_config.Tasks.Prune)
            return;

        foreach (PartitionConfig partition in _partition.GetPartitions())
            await PrunePartition(partition);
    }
}
