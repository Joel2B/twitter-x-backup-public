using Backup.App.Interfaces;
using Backup.App.Interfaces.Data.Posts;
using Backup.App.Interfaces.Partition;
using Backup.App.Models.Config.Data;
using Backup.App.Models.Config.Data.Posts;
using Backup.App.Models.Posts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Backup.Infrastructure.Data.Posts;

public partial class SqlitePostData(
    ILogger<SqlitePostData> logger,
    StoragePost config,
    IPartition partition
) : IPostDataStore, ISetup, IAsyncDisposable
{
    public string? Id { get; set; }
    public bool IsDefault { get; set; }

    private readonly ILogger<SqlitePostData> _logger = logger;
    private readonly StoragePost _config = config;
    private readonly IPartition _partition = partition;
    private PostsDbContext? _db;
    private const int SqlInChunkSize = 5000;

    public async Task Setup()
    {
        foreach (PartitionConfig p in _partition.GetPartitions())
            Directory.CreateDirectory(GetBasePath(p));

        await EnsureSchema();
    }

    public async Task<List<Post>?> GetAll()
    {
        PostsDbContext db = await GetDbContext();

        List<PostEntity> entities = await IncludePostGraph(db.Posts)
            .AsNoTracking()
            .AsSplitQuery()
            .OrderBy(o => o.CreatedAt)
            .ToListAsync();

        Dictionary<string, bool> deletedById = await LoadDeletedByIds(
            db,
            entities.Select(entity => entity.Id)
        );

        List<Post> posts = entities
            .Select(entity => ToModel(entity, GetDeleted(deletedById, entity.Id)))
            .ToList();

        return posts.Count == 0 ? null : posts;
    }

    public async Task<int> GetCount()
    {
        PostsDbContext db = await GetDbContext();
        return await db.Posts.CountAsync();
    }

    public async Task<PostStoreCounts> GetStoreCounts()
    {
        PostsDbContext db = await GetDbContext();

        return new PostStoreCounts
        {
            Posts = await db.Posts.CountAsync(),
            Profiles = await db.Profiles.CountAsync(),
            Hashtags = await db.PostHashtags.CountAsync(),
            Medias = await db.PostMedias.CountAsync(),
            MediaVariants = await db.PostMediaVariants.CountAsync(),
            IndexEntries = await db.PostIndexEntries.CountAsync(),
            Changes = await db.PostChanges.CountAsync(),
            ChangeFields = await db.PostChangeFields.CountAsync(),
            HashMeta = await db.PostHashMeta.CountAsync(),
        };
    }

    public async Task<List<MediaInput>?> GetMediaInputs()
    {
        PostsDbContext db = await GetDbContext();

        List<PostEntity> entities = await IncludePostMediaGraph(db.Posts)
            .AsNoTracking()
            .AsSplitQuery()
            .OrderBy(o => o.CreatedAt)
            .ToListAsync();

        if (entities.Count == 0)
            return null;

        Dictionary<string, bool> deletedById = await LoadDeletedByIds(
            db,
            entities.Select(entity => entity.Id)
        );

        List<MediaInput> current = entities
            .Select(entity => ToMediaInput(entity, GetDeleted(deletedById, entity.Id)))
            .ToList();

        List<MediaInput> history = [];

        foreach (PostEntity entity in entities)
        {
            if (entity.Changes.Count == 0)
                continue;

            Post post = ToModel(entity, GetDeleted(deletedById, entity.Id));

            history.AddRange(
                post.Changes.Where(change => change.Data is not null)
                    .Select(change => ToMediaInput(change.Data!))
            );
        }

        current.AddRange(history);
        return current;
    }

    public async Task<Dictionary<string, string>> GetHashesById()
    {
        PostsDbContext db = await GetDbContext();

        return await db
            .PostHashMeta.AsNoTracking()
            .ToDictionaryAsync(row => row.Id, row => row.Hash, StringComparer.Ordinal);
    }

    public async Task<List<Post>> GetByIds(IReadOnlyCollection<string> ids)
    {
        if (ids.Count == 0)
            return [];

        HashSet<string> filter = ids.Where(id => !string.IsNullOrWhiteSpace(id))
            .ToHashSet(StringComparer.Ordinal);

        if (filter.Count == 0)
            return [];

        PostsDbContext db = await GetDbContext();
        List<PostEntity> posts = await LoadPostGraphByIds(db, [.. filter]);

        Dictionary<string, bool> deletedById = await LoadDeletedByIds(
            db,
            posts.Select(post => post.Id)
        );

        return posts
            .Select(post => ToModel(post, GetDeleted(deletedById, post.Id)).Clone())
            .ToList();
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

        PostsDbContext db = await GetDbContext();
        Dictionary<string, int> totals = new(StringComparer.Ordinal);

        foreach (List<string> chunk in ChunkStrings(filter))
        {
            List<KeyValuePair<string, int>> rows = await db
                .Posts.AsNoTracking()
                .Where(post => chunk.Contains(post.ProfileId))
                .GroupBy(post => post.ProfileId)
                .Select(group => new KeyValuePair<string, int>(group.Key, group.Count()))
                .ToListAsync();

            foreach (KeyValuePair<string, int> row in rows)
                totals[row.Key] = totals.TryGetValue(row.Key, out int count)
                    ? count + row.Value
                    : row.Value;
        }

        return totals;
    }

    public async Task AddPosts(
        string userId,
        string origin,
        List<Post> incoming,
        MergeOptions? options = null
    )
    {
        options ??= new();
        PostsDbContext db = await GetDbContext();

        List<Post> posts = incoming.Where(post => !string.IsNullOrWhiteSpace(post.Id)).ToList();

        if (posts.Count == 0)
            return;

        HashSet<string> ids = posts.Select(post => post.Id).ToHashSet(StringComparer.Ordinal);
        List<PostEntity> existingEntities = await LoadPostGraphByIds(db, [.. ids]);

        Dictionary<string, bool> deletedById = await LoadDeletedByIds(
            db,
            existingEntities.Select(post => post.Id)
        );

        Dictionary<string, Post> existingPosts = existingEntities.ToDictionary(
            post => post.Id,
            post => ToModel(post, GetDeleted(deletedById, post.Id)),
            StringComparer.Ordinal
        );

        List<Post> resolved = [];

        foreach (Post result in posts)
        {
            if (!existingPosts.TryGetValue(result.Id, out Post? current))
            {
                resolved.Add(result.Clone());
                continue;
            }

            Post merged = result.Clone();
            Change change = new() { UserId = userId };
            MergeData(current, merged, change);

            IndexData? incomingIndexData = null;

            if (options.Index)
            {
                Dictionary<string, IndexData> incomingUserIndex = merged.Index.TryGetValue(
                    userId,
                    out Dictionary<string, IndexData>? userIndex
                )
                    ? userIndex
                    : [];

                incomingIndexData = incomingUserIndex.TryGetValue(origin, out IndexData? indexData)
                    ? indexData.Clone()
                    : new();
            }

            merged.Index = current.CloneIndex();
            merged.Changes = current.CloneChanges();

            if (options.Index)
            {
                if (!merged.Index.ContainsKey(userId))
                    merged.Index[userId] = [];

                merged.Index[userId][origin] = incomingIndexData ?? new();
                MergeIndex(current, merged, change, origin);
            }

            bool hasDataChange = change.Data is not null;
            bool hasIndexChange = options.Index && change.Index is not null;

            if (!hasDataChange && !hasIndexChange)
                continue;

            if (change.Data is not null || change.Index is not null)
                merged.Changes.Add(change);

            resolved.Add(merged);
        }

        if (resolved.Count == 0)
            return;

        await UpsertPosts(resolved);
    }

    public async Task<int> MarkDeletedExcept(
        string userId,
        string origin,
        IReadOnlyCollection<string> keepPostIds
    )
    {
        PostsDbContext db = await GetDbContext();

        HashSet<string> keep = keepPostIds
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .ToHashSet(StringComparer.Ordinal);

        List<string> scopedIds = await db
            .PostIndexEntries.AsNoTracking()
            .Where(entry => entry.UserId == userId && entry.Origin == origin)
            .Select(entry => entry.PostId)
            .Distinct()
            .ToListAsync();

        if (scopedIds.Count == 0)
            return 0;

        if (keep.Count > 0)
            scopedIds = scopedIds.Where(id => !keep.Contains(id)).ToList();

        if (scopedIds.Count == 0)
            return 0;

        Dictionary<string, PostHashMetaEntity> metaById = new(StringComparer.Ordinal);

        foreach (List<string> chunk in ChunkStrings(scopedIds))
        {
            List<PostHashMetaEntity> rows = await db
                .PostHashMeta.Where(row => chunk.Contains(row.Id) && !row.Deleted)
                .ToListAsync();

            foreach (PostHashMetaEntity row in rows)
                metaById[row.Id] = row;
        }

        if (metaById.Count == 0)
            return 0;

        List<PostEntity> sourceEntities = await LoadPostGraphByIds(db, [.. metaById.Keys]);

        if (sourceEntities.Count == 0)
            return 0;

        int marked = 0;
        DateTime changeDate = DateTime.Now;

        foreach (PostEntity entity in sourceEntities)
        {
            if (!metaById.TryGetValue(entity.Id, out PostHashMetaEntity? meta))
                continue;

            if (meta.Deleted)
                continue;

            PostChangeEntity change = new()
            {
                PostId = entity.Id,
                UserId = userId,
                Date = changeDate,
                ChangeType = "data_update",
                Fields =
                [
                    new PostChangeFieldEntity
                    {
                        Field = ChangeFields.PostDeleted,
                        OldValueJson = SerializeJson(false),
                        NewValueJson = SerializeJson(true),
                    },
                ],
            };

            db.PostChanges.Add(change);
            meta.Deleted = true;
            meta.Hash = Backup.App.Utils.PostHash.Compute(ToModel(entity, deleted: true));
            marked++;
        }

        return marked;
    }

    public Task Reset(List<Post> posts)
    {
        return ResetInternal(posts);
    }

    public async Task UpsertPosts(List<Post> posts)
    {
        if (posts.Count == 0)
            return;

        PostsDbContext db = await GetDbContext();

        List<Post> normalizedPosts = posts
            .Where(post => !string.IsNullOrWhiteSpace(post.Id))
            .GroupBy(post => post.Id, StringComparer.Ordinal)
            .Select(group => group.Last().Clone())
            .ToList();

        if (normalizedPosts.Count == 0)
            return;

        List<string> ids = normalizedPosts.Select(post => post.Id).ToList();
        DetachTrackedPostGraphByIds(db, ids);

        await using var tx = await db.Database.BeginTransactionAsync();

        try
        {
            await DeletePostGraphByIds(db, ids);

            HashSet<string> profileIds = normalizedPosts
                .Select(post => post.Profile.Id)
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .ToHashSet(StringComparer.Ordinal);

            Dictionary<string, PostProfileEntity> profilesById = await LoadProfilesByIds(
                db,
                [.. profileIds]
            );

            List<PostEntity> batch = [];

            foreach (Post post in normalizedPosts)
            {
                PostProfileEntity profileEntity = GetOrCreateProfileEntity(
                    db,
                    profilesById,
                    post.Profile
                );

                PostEntity created = ToEntity(post, profileEntity);
                batch.Add(created);

                if (batch.Count < SqlInChunkSize)
                    continue;

                db.Posts.AddRange(batch);
                batch.Clear();
            }

            if (batch.Count > 0)
                db.Posts.AddRange(batch);

            await UpsertHashMetaForPosts(db, normalizedPosts);
            await db.SaveChangesAsync();
            await tx.CommitAsync();
            db.ChangeTracker.Clear();
        }
        catch (Exception ex)
        {
            string incomingIdsSample = JoinSample(ids);
            string trackedSummary = BuildTrackedGraphSummary(db, ids);

            _logger.LogError(
                ex,
                "sqlite upsert failed for post ids [{ids}] | {trackedSummary}",
                incomingIdsSample,
                trackedSummary
            );

            await tx.RollbackAsync();
            db.ChangeTracker.Clear();
            throw;
        }
    }

    public async Task Save()
    {
        PostsDbContext db = await GetDbContext();

        if (db.ChangeTracker.HasChanges())
        {
            await db.SaveChangesAsync();
            db.ChangeTracker.Clear();
        }
        else
        {
            _logger.LogInformation("sqlite save skipped: no tracked changes");
        }

        try
        {
            await Replicate();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "sqlite replicate failed; continuing without replica sync");
        }

        _logger.LogInformation("sqlite save complete: tracked changes persisted");
    }

    public Task Prune()
    {
        _logger.LogInformation("running prune");
        _logger.LogInformation("prune: {value}", _config.Tasks.Prune);

        if (!_config.Tasks.Prune)
            return Task.CompletedTask;

        _logger.LogInformation("sqlite prune skipped: posts are retained as soft-delete.");
        return Task.CompletedTask;
    }

    public async ValueTask DisposeAsync()
    {
        if (_db is not null)
        {
            await _db.DisposeAsync();
            _db = null;
        }

        GC.SuppressFinalize(this);
    }
}

