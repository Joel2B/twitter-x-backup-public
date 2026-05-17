using Backup.App.Extensions;
using Backup.App.Interfaces.Data.Post;
using Backup.App.Interfaces.Services.Post;
using Microsoft.Extensions.Logging;

namespace Backup.App.Data.Post;

public class PostDataMultiStore(
    IEnumerable<IPostDataStore> stores,
    IPostReplication replication,
    ILogger<PostDataMultiStore> logger
) : IPostData
{
    private readonly List<IPostDataStore> _stores = [.. stores];
    private readonly IPostReplication _replication = replication;
    private readonly ILogger<PostDataMultiStore> _logger = logger;

    private IPostDataStore Primary
    {
        get
        {
            if (_stores.Count == 0)
                throw new InvalidOperationException("No post data stores are configured.");

            List<IPostDataStore> defaults = _stores.Where(store => store.IsDefault).ToList();

            if (defaults.Count > 1)
                throw new InvalidOperationException(
                    "Only one post data store can be marked as default."
                );

            return defaults.FirstOrDefault() ?? _stores.First();
        }
    }

    public string? Id
    {
        get => Primary.Id;
        set => Primary.Id = value;
    }

    public Task<int> GetCount() => Primary.GetCount();

    public Task<List<Models.Post.Post>?> GetAll() => Primary.GetAll();

    public Task<List<Models.Post.MediaInput>?> GetMediaInputs() => Primary.GetMediaInputs();

    public Task<Dictionary<string, string>> GetHashesById() => Primary.GetHashesById();

    public Task<List<Models.Post.Post>> GetByIds(IReadOnlyCollection<string> ids) =>
        Primary.GetByIds(ids);

    public Task<Dictionary<string, int>> GetPostCountsByProfileIds(
        IReadOnlyCollection<string> profileIds
    ) => Primary.GetPostCountsByProfileIds(profileIds);

    public Task AddPosts(
        string userId,
        string origin,
        List<Models.Post.Post> incoming,
        Models.Post.MergeOptions? options = null
    ) => Primary.AddPosts(userId, origin, incoming, options);

    public Task<int> MarkDeletedExcept(
        string userId,
        string origin,
        IReadOnlyCollection<string> keepPostIds
    ) => Primary.MarkDeletedExcept(userId, origin, keepPostIds);

    public Task Reset(List<Models.Post.Post> posts) => Primary.Reset(posts);

    public Task UpsertPosts(List<Models.Post.Post> posts) => Primary.UpsertPosts(posts);

    public async Task Save()
    {
        await Primary.Save();

        if (_stores.Count <= 1)
            return;

        await _replication.Replicate(_stores);
    }

    public Task Prune() => Primary.Prune();

    public async Task VerifyStoreCounts()
    {
        if (_stores.Count <= 1)
        {
            _logger.LogInfo(
                "post store parity skipped: only {count} enabled post store",
                _stores.Count
            );
            return;
        }

        Dictionary<IPostDataStore, Models.Post.PostStoreCounts> countsByStore = [];

        foreach (IPostDataStore store in _stores)
        {
            Models.Post.PostStoreCounts counts = await store.GetStoreCounts();
            countsByStore[store] = counts;

            _logger.LogInfo(
                "post store counts [{storeId}] posts={posts}, profiles={profiles}, hashtags={hashtags}, medias={medias}, mediaVariants={mediaVariants}, indexEntries={indexEntries}, changes={changes}, changeFields={changeFields}, hashMeta={hashMeta}",
                GetStoreLabel(store),
                counts.Posts,
                counts.Profiles,
                counts.Hashtags,
                counts.Medias,
                counts.MediaVariants,
                counts.IndexEntries,
                counts.Changes,
                counts.ChangeFields,
                counts.HashMeta
            );
        }

        IPostDataStore primary = Primary;
        Models.Post.PostStoreCounts primaryCounts = countsByStore[primary];

        foreach (IPostDataStore store in _stores.Where(store => !ReferenceEquals(store, primary)))
        {
            Models.Post.PostStoreCounts candidateCounts = countsByStore[store];
            List<string> diffs = GetCountDiffs(primaryCounts, candidateCounts);

            if (diffs.Count == 0)
            {
                _logger.LogInfo(
                    "post store parity OK: primary={primary} secondary={secondary}",
                    GetStoreLabel(primary),
                    GetStoreLabel(store)
                );
                continue;
            }

            _logger.LogWarning(
                "post store parity MISMATCH: primary={primary} secondary={secondary} diffs={diffs}",
                GetStoreLabel(primary),
                GetStoreLabel(store),
                string.Join(", ", diffs)
            );
        }
    }

    private static string GetStoreLabel(IPostDataStore store) =>
        string.IsNullOrWhiteSpace(store.Id) ? store.GetType().Name : store.Id!;

    private static List<string> GetCountDiffs(
        Models.Post.PostStoreCounts left,
        Models.Post.PostStoreCounts right
    )
    {
        List<string> diffs = [];
        AddDiff("posts", left.Posts, right.Posts);
        AddDiff("profiles", left.Profiles, right.Profiles);
        AddDiff("hashtags", left.Hashtags, right.Hashtags);
        AddDiff("medias", left.Medias, right.Medias);
        AddDiff("mediaVariants", left.MediaVariants, right.MediaVariants);
        AddDiff("indexEntries", left.IndexEntries, right.IndexEntries);
        AddDiff("changes", left.Changes, right.Changes);
        AddDiff("changeFields", left.ChangeFields, right.ChangeFields);
        AddDiff("hashMeta", left.HashMeta, right.HashMeta);
        return diffs;

        void AddDiff(string name, int a, int b)
        {
            if (a == b)
                return;

            diffs.Add($"{name}:{a}!={b}");
        }
    }
}
