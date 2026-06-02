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
using Backup.Infrastructure.Posts.Models.Stored;
using Microsoft.Extensions.Logging;

namespace Backup.Infrastructure.Posts.Data.Json;

// Facade for JSON-backed post storage. Public behavior should remain stable;
// internal responsibilities are intentionally delegated to collaborators.
public partial class LocalPostData : IPostDataStore, ISetup
{
    public string? Id { get; set; }
    public bool IsDefault { get; set; }

    private readonly ILogger<LocalPostData> _logger;
    private readonly AppConfig _appConfig;
    private readonly StoragePost _config;
    private readonly IPartition _partition;
    private readonly LocalPostDataMutationCoordinator _mutationCoordinator;
    private readonly LocalPostDataReadCoordinator _readCoordinator;
    private readonly LocalPostDataHashCoordinator _hashCoordinator;
    private readonly LocalPostDataHistoryCoordinator _historyCoordinator;
    private readonly LocalPostDataTableCoordinator _tableCoordinator;
    private readonly IDataStoreGuardService _dataStoreGuardService;

    private Dictionary<string, Post>? _postsCache = null;
    private Dictionary<string, PostMetaRow>? _postMetaCache = null;

    internal LocalPostData(
        ILogger<LocalPostData> logger,
        AppConfig appConfig,
        StoragePost config,
        IPartition partition,
        LocalPostDataMutationCoordinator mutationCoordinator,
        LocalPostDataReadCoordinator readCoordinator,
        LocalPostDataHashCoordinator hashCoordinator,
        LocalPostDataHistoryCoordinator historyCoordinator,
        LocalPostDataTableCoordinator tableCoordinator,
        IDataStoreGuardService dataStoreGuardService
    )
    {
        _logger = logger;
        _appConfig = appConfig;
        _config = config;
        _partition = partition;
        _mutationCoordinator = mutationCoordinator;
        _readCoordinator = readCoordinator;
        _hashCoordinator = hashCoordinator;
        _historyCoordinator = historyCoordinator;
        _tableCoordinator = tableCoordinator;
        _dataStoreGuardService = dataStoreGuardService;
    }

    public Task Setup()
    {
        _logger.LogInformation("setup: starting");
        SetupDirectory();
        _logger.LogInformation("setup: completed");

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

        _hashCoordinator.EnsureParity(postCount, metaCount, Id ?? _config.Id ?? "unknown");

        return postMeta.ToDictionary(
            entry => entry.Key,
            entry => entry.Value.Hash,
            StringComparer.Ordinal
        );
    }

    public async Task<List<Post>> GetByIds(IReadOnlyCollection<string> ids)
    {
        IReadOnlySet<string> filter = _readCoordinator.NormalizeIdentifiers(ids);

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

        Backup.Domain.Posts.PostStoreCounts counts = _readCoordinator.ComputeStoreCounts(
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

        IReadOnlySet<string> idsToDelete = _mutationCoordinator.SelectIdsToMarkDeleted(
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
            _readCoordinator.ComposeMediaInputs(domainPosts);

        return composed.Select(PostReplicationMapper.ToApp).ToList();
    }

    public async Task<Dictionary<string, int>> GetPostCountsByProfileIds(
        IReadOnlyCollection<string> profileIds
    )
    {
        IReadOnlySet<string> filter = _readCoordinator.NormalizeIdentifiers(profileIds);

        if (filter.Count == 0)
            return [];

        Dictionary<string, Post>? cache = await GetCache();
        if (cache is null)
            return [];

        IReadOnlyDictionary<string, int> counts = _readCoordinator.CountByProfileIds(
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
        {
            _logger.LogInformation("save: skipped, cache is empty");
            return;
        }

        _logger.LogInformation("save: starting for {count} posts", _postsCache.Count);
        List<Post> posts = [.. _postsCache.Values];

        _logger.LogInformation("save: building tables");
        LocalPostTables tables = BuildTables(posts);

        _logger.LogInformation("save: ensuring post meta cache");
        Dictionary<string, PostMetaRow> postMeta = await EnsurePostMetaCache(posts);

        _logger.LogInformation("save: writing tables");
        await SaveTables(tables, postMeta);

        _logger.LogInformation("save: replicating tables");
        Replicate();

        SetCache(posts);
        _postMetaCache = postMeta;
        _logger.LogInformation("save: completed with {count} posts", posts.Count);
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
