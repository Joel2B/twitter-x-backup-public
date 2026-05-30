using Backup.Infrastructure.Core.Abstractions.Setup;
using Backup.Infrastructure.Posts.Abstractions.Data;
using Backup.Infrastructure.Core.Abstractions.Partition;
using Backup.Infrastructure.Models.Config;
using Backup.Infrastructure.Models.Config.Data;
using Backup.Infrastructure.Models.Config.Data.Posts;
using Backup.Infrastructure.Models.Data.Json;
using Backup.Infrastructure.Posts.Adapters;
using Backup.Infrastructure.Posts.Models;
using Backup.Application.Posts;
using Microsoft.Extensions.Logging;

namespace Backup.Infrastructure.Posts.Data.Json;

public partial class LocalPostData(
    ILogger<LocalPostData> _logger,
    AppConfig _appConfig,
    StoragePost _config,
    IPartition _partition,
    IPostMergeService postMergeService,
    IPostSoftDeleteSelectionService postSoftDeleteSelectionService,
    IPostSnapshotNormalizationService postSnapshotNormalizationService,
    IPostMediaInputsCompositionService postMediaInputsCompositionService,
    IPostHashingService postHashingService,
    IPostHistoryPrunePolicyService postHistoryPrunePolicyService,
    IPostSnapshotSizeGuardService postSnapshotSizeGuardService
) : IPostDataStore, ISetup
{
    public string? Id { get; set; }
    public bool IsDefault { get; set; }

    private readonly ILogger<LocalPostData> _logger = _logger;
    private readonly AppConfig _appConfig = _appConfig;
    private readonly StoragePost _config = _config;
    private readonly IPartition _partition = _partition;
    private readonly IPostMergeService _postMergeService = postMergeService;
    private readonly IPostSoftDeleteSelectionService _postSoftDeleteSelectionService =
        postSoftDeleteSelectionService;
    private readonly IPostSnapshotNormalizationService _postSnapshotNormalizationService =
        postSnapshotNormalizationService;
    private readonly IPostMediaInputsCompositionService _postMediaInputsCompositionService =
        postMediaInputsCompositionService;
    private readonly IPostHashingService _postHashingService = postHashingService;
    private readonly IPostHistoryPrunePolicyService _postHistoryPrunePolicyService =
        postHistoryPrunePolicyService;
    private readonly IPostSnapshotSizeGuardService _postSnapshotSizeGuardService =
        postSnapshotSizeGuardService;

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

        if (postCount != metaCount)
        {
            throw new InvalidOperationException(
                $"post_meta count mismatch in local post store '{Id ?? _config.Id ?? "unknown"}': posts={postCount}, post_meta={metaCount}"
            );
        }

        return postMeta.ToDictionary(
            entry => entry.Key,
            entry => entry.Value.Hash,
            StringComparer.Ordinal
        );
    }

    public async Task<List<Post>> GetByIds(IReadOnlyCollection<string> ids)
    {
        if (ids.Count == 0)
            return [];

        HashSet<string> filter = ids.Where(id => !string.IsNullOrWhiteSpace(id))
            .ToHashSet(StringComparer.Ordinal);

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

        if (cache is null || cache.Count == 0)
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
                HashMeta = _postMetaCache?.Count ?? 0,
            };
        }

        int hashtags = 0;
        int medias = 0;
        int mediaVariants = 0;
        int indexEntries = 0;
        int changes = 0;
        int changeFields = 0;

        foreach (Post post in cache.Values)
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

            List<Change> orderedChanges = post
                .Changes.Select((change, index) => new { Change = change, Index = index })
                .OrderBy(o => o.Change.Date)
                .ThenBy(o => o.Index)
                .Select(o => o.Change)
                .ToList();

            for (int i = 0; i < orderedChanges.Count; i++)
            {
                List<PostChangeFieldRow> fields = BuildChangeFields(post, orderedChanges, i);

                if (fields.Count == 0)
                    continue;

                changes++;
                changeFields += fields.Count;
            }
        }

        return new PostStoreCounts
        {
            Posts = cache.Count,
            Profiles = cache
                .Values.Select(post => post.Profile.Id)
                .Distinct(StringComparer.Ordinal)
                .Count(),
            Hashtags = hashtags,
            Medias = medias,
            MediaVariants = mediaVariants,
            IndexEntries = indexEntries,
            Changes = changes,
            ChangeFields = changeFields,
            HashMeta = _postMetaCache?.Count ?? cache.Count,
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

        IReadOnlyCollection<string> idsToDelete = _postSoftDeleteSelectionService.SelectIds(
            userId,
            origin,
            keepPostIds,
            domainPosts
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

        IReadOnlyList<Backup.Domain.Posts.MediaInput> composed = _postMediaInputsCompositionService.Compose(
            domainPosts
        );

        return composed.Select(PostReplicationMapper.ToApp).ToList();
    }

    public async Task<Dictionary<string, int>> GetPostCountsByProfileIds(
        IReadOnlyCollection<string> profileIds
    )
    {
        if (profileIds.Count == 0)
            return [];

        HashSet<string> filter = profileIds
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .ToHashSet(StringComparer.Ordinal);

        if (filter.Count == 0)
            return [];

        static Dictionary<string, int> CountByProfileIds(
            IEnumerable<string> profileIds,
            HashSet<string> filter
        ) =>
            profileIds
                .Where(filter.Contains)
                .GroupBy(profileId => profileId, StringComparer.Ordinal)
                .ToDictionary(group => group.Key, group => group.Count(), StringComparer.Ordinal);

        Dictionary<string, Post>? cache = await GetCache();
        if (cache is null)
            return [];

        return CountByProfileIds(cache.Values.Select(post => post.Profile.Id), filter);
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
