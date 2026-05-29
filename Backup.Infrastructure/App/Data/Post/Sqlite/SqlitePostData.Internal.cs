using Backup.App.Models.Posts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Backup.Infrastructure.Data.Posts;

public partial class SqlitePostData
{
    private async Task<PostsDbContext> GetDbContext()
    {
        if (_db is not null)
            return _db;

        string dbPath = GetDatabasePath();
        _db = CreateDbContext(dbPath);

        return _db;
    }

    private static IEnumerable<List<string>> ChunkStrings(
        IEnumerable<string> values,
        int size = SqlInChunkSize
    )
    {
        if (size <= 0)
            throw new ArgumentOutOfRangeException(nameof(size));

        List<string> buffer = new(size);

        foreach (string value in values)
        {
            if (string.IsNullOrWhiteSpace(value))
                continue;

            buffer.Add(value);

            if (buffer.Count < size)
                continue;

            yield return buffer;
            buffer = new List<string>(size);
        }

        if (buffer.Count > 0)
            yield return buffer;
    }

    private static IQueryable<PostEntity> IncludePostGraph(IQueryable<PostEntity> query) =>
        query
            .Include(o => o.Profile)
            .Include(o => o.Hashtags)
            .Include(o => o.Medias)
            .ThenInclude(o => o.Variants)
            .Include(o => o.Changes)
            .ThenInclude(o => o.Fields)
            .Include(o => o.IndexEntries);

    private static IQueryable<PostEntity> IncludePostMediaGraph(IQueryable<PostEntity> query) =>
        query
            .Include(o => o.Profile)
            .Include(o => o.Medias)
            .ThenInclude(o => o.Variants)
            .Include(o =>
                o.Changes.Where(change =>
                    change.ChangeType == "data_update" || change.ChangeType == "data_index_update"
                )
            )
            .ThenInclude(o => o.Fields);

    private static async Task<List<PostEntity>> LoadPostGraphByIds(
        PostsDbContext db,
        IReadOnlyCollection<string> ids
    )
    {
        if (ids.Count == 0)
            return [];

        List<PostEntity> result = [];

        foreach (List<string> chunk in ChunkStrings(ids))
        {
            IQueryable<PostEntity> query = IncludePostGraph(db.Posts)
                .AsSplitQuery()
                .Where(post => chunk.Contains(post.Id))
                .AsNoTracking();

            List<PostEntity> rows = await query.ToListAsync();

            if (rows.Count > 0)
                result.AddRange(rows);
        }

        return result;
    }

    private static async Task<Dictionary<string, bool>> LoadDeletedByIds(
        PostsDbContext db,
        IEnumerable<string> ids
    )
    {
        List<string> normalized = ids.Where(id => !string.IsNullOrWhiteSpace(id))
            .Distinct(StringComparer.Ordinal)
            .ToList();

        if (normalized.Count == 0)
            return [];

        Dictionary<string, bool> result = new(StringComparer.Ordinal);

        foreach (List<string> chunk in ChunkStrings(normalized))
        {
            List<PostHashMetaEntity> rows = await db
                .PostHashMeta.AsNoTracking()
                .Where(row => chunk.Contains(row.Id))
                .ToListAsync();

            foreach (PostHashMetaEntity row in rows)
                result[row.Id] = row.Deleted;
        }

        return result;
    }

    private static bool GetDeleted(IReadOnlyDictionary<string, bool> deletedById, string id) =>
        deletedById.TryGetValue(id, out bool deleted) && deleted;

    private static MediaInput ToMediaInput(PostEntity entity, bool deleted) =>
        new()
        {
            Id = entity.Id,
            Profile = new()
            {
                Id = entity.Profile.Id,
                UserName = entity.Profile.UserName,
                Name = entity.Profile.Name,
                BannerUrl = entity.Profile.BannerUrl,
                ImageUrl = entity.Profile.ImageUrl,
                Following = entity.Profile.Following,
                Count = entity.Profile.CountMedia.HasValue
                    ? new PostCount { Media = entity.Profile.CountMedia }
                    : null,
            },
            Medias = entity
                .Medias.OrderBy(o => o.Ordinal)
                .Select(ToModel)
                .Select(media => media.Clone())
                .ToList(),
            Deleted = deleted,
        };

    private static MediaInput ToMediaInput(PostData data) =>
        new()
        {
            Id = data.Id,
            Profile = data.Profile.Clone(),
            Medias = data.Medias?.Select(media => media.Clone()).ToList(),
            Deleted = data.Deleted,
        };

    private static async Task DeletePostGraphByIds(
        PostsDbContext db,
        IReadOnlyCollection<string> ids
    )
    {
        if (ids.Count == 0)
            return;

        foreach (List<string> chunk in ChunkStrings(ids))
        {
            IQueryable<int> changeIds = db
                .PostChanges.Where(change => chunk.Contains(change.PostId))
                .Select(change => change.Id);

            IQueryable<int> mediaIds = db
                .PostMedias.Where(media => chunk.Contains(media.PostId))
                .Select(media => media.Id);

            await db
                .PostChangeFields.Where(field => changeIds.Contains(field.ChangeId))
                .ExecuteDeleteAsync();

            await db
                .PostMediaVariants.Where(variant => mediaIds.Contains(variant.MediaRefId))
                .ExecuteDeleteAsync();

            await db
                .PostChanges.Where(change => chunk.Contains(change.PostId))
                .ExecuteDeleteAsync();

            await db.PostMedias.Where(media => chunk.Contains(media.PostId)).ExecuteDeleteAsync();

            await db
                .PostHashtags.Where(hashtag => chunk.Contains(hashtag.PostId))
                .ExecuteDeleteAsync();

            await db
                .PostIndexEntries.Where(entry => chunk.Contains(entry.PostId))
                .ExecuteDeleteAsync();

            await db.Posts.Where(post => chunk.Contains(post.Id)).ExecuteDeleteAsync();
            await db.PostHashMeta.Where(row => chunk.Contains(row.Id)).ExecuteDeleteAsync();
        }
    }

    private static void DetachTrackedPostGraphByIds(PostsDbContext db, IReadOnlyCollection<string> ids)
    {
        if (ids.Count == 0)
            return;

        HashSet<string> idSet = ids.Where(id => !string.IsNullOrWhiteSpace(id))
            .ToHashSet(StringComparer.Ordinal);

        if (idSet.Count == 0)
            return;

        HashSet<int> mediaIds = db
            .ChangeTracker.Entries<PostMediaEntity>()
            .Where(entry => idSet.Contains(entry.Entity.PostId))
            .Select(entry => entry.Entity.Id)
            .ToHashSet();

        if (mediaIds.Count > 0)
        {
            foreach (
                var entry in db
                    .ChangeTracker.Entries<PostMediaVariantEntity>()
                    .Where(entry => mediaIds.Contains(entry.Entity.MediaRefId))
                    .ToList()
            )
            {
                entry.State = EntityState.Detached;
            }
        }

        HashSet<int> changeIds = db
            .ChangeTracker.Entries<PostChangeEntity>()
            .Where(entry => idSet.Contains(entry.Entity.PostId))
            .Select(entry => entry.Entity.Id)
            .ToHashSet();

        if (changeIds.Count > 0)
        {
            foreach (
                var entry in db
                    .ChangeTracker.Entries<PostChangeFieldEntity>()
                    .Where(entry => changeIds.Contains(entry.Entity.ChangeId))
                    .ToList()
            )
            {
                entry.State = EntityState.Detached;
            }
        }

        foreach (
            var entry in db
                .ChangeTracker.Entries<PostHashtagEntity>()
                .Where(entry => idSet.Contains(entry.Entity.PostId))
                .ToList()
        )
        {
            entry.State = EntityState.Detached;
        }

        foreach (
            var entry in db
                .ChangeTracker.Entries<PostMediaEntity>()
                .Where(entry => idSet.Contains(entry.Entity.PostId))
                .ToList()
        )
        {
            entry.State = EntityState.Detached;
        }

        foreach (
            var entry in db
                .ChangeTracker.Entries<PostIndexEntryEntity>()
                .Where(entry => idSet.Contains(entry.Entity.PostId))
                .ToList()
        )
        {
            entry.State = EntityState.Detached;
        }

        foreach (
            var entry in db
                .ChangeTracker.Entries<PostChangeEntity>()
                .Where(entry => idSet.Contains(entry.Entity.PostId))
                .ToList()
        )
        {
            entry.State = EntityState.Detached;
        }

        foreach (
            var entry in db
                .ChangeTracker.Entries<PostHashMetaEntity>()
                .Where(entry => idSet.Contains(entry.Entity.Id))
                .ToList()
        )
        {
            entry.State = EntityState.Detached;
        }

        foreach (
            var entry in db
                .ChangeTracker.Entries<PostEntity>()
                .Where(entry => idSet.Contains(entry.Entity.Id))
                .ToList()
        )
        {
            entry.State = EntityState.Detached;
        }
    }

    private static string BuildTrackedGraphSummary(PostsDbContext db, IReadOnlyCollection<string> ids)
    {
        HashSet<string> idSet = ids.Where(id => !string.IsNullOrWhiteSpace(id))
            .ToHashSet(StringComparer.Ordinal);

        if (idSet.Count == 0)
            return "ids=0";

        List<string> trackedPostIds = db
            .ChangeTracker.Entries<PostEntity>()
            .Where(entry => idSet.Contains(entry.Entity.Id))
            .Select(entry => entry.Entity.Id)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(id => id, StringComparer.Ordinal)
            .ToList();

        int trackedHashtags = db
            .ChangeTracker.Entries<PostHashtagEntity>()
            .Count(entry => idSet.Contains(entry.Entity.PostId));

        List<PostMediaEntity> trackedMedias = db
            .ChangeTracker.Entries<PostMediaEntity>()
            .Where(entry => idSet.Contains(entry.Entity.PostId))
            .Select(entry => entry.Entity)
            .ToList();

        int trackedMediaCount = trackedMedias.Count;
        HashSet<int> trackedMediaIds = trackedMedias.Select(media => media.Id).ToHashSet();

        int trackedVariants = db
            .ChangeTracker.Entries<PostMediaVariantEntity>()
            .Count(entry => trackedMediaIds.Contains(entry.Entity.MediaRefId));

        List<PostChangeEntity> trackedChanges = db
            .ChangeTracker.Entries<PostChangeEntity>()
            .Where(entry => idSet.Contains(entry.Entity.PostId))
            .Select(entry => entry.Entity)
            .ToList();

        int trackedChangeCount = trackedChanges.Count;
        HashSet<int> trackedChangeIds = trackedChanges.Select(change => change.Id).ToHashSet();

        int trackedChangeFields = db
            .ChangeTracker.Entries<PostChangeFieldEntity>()
            .Count(entry => trackedChangeIds.Contains(entry.Entity.ChangeId));

        int trackedIndexEntries = db
            .ChangeTracker.Entries<PostIndexEntryEntity>()
            .Count(entry => idSet.Contains(entry.Entity.PostId));

        int trackedHashMeta = db
            .ChangeTracker.Entries<PostHashMetaEntity>()
            .Count(entry => idSet.Contains(entry.Entity.Id));

        int totalTracked = db.ChangeTracker.Entries().Count();

        return string.Join(
            ", ",
            [
                $"incomingIds={idSet.Count}",
                $"trackedPosts={trackedPostIds.Count}",
                $"trackedPostIds=[{JoinSample(trackedPostIds)}]",
                $"trackedHashtags={trackedHashtags}",
                $"trackedMedias={trackedMediaCount}",
                $"trackedVariants={trackedVariants}",
                $"trackedChanges={trackedChangeCount}",
                $"trackedChangeFields={trackedChangeFields}",
                $"trackedIndexEntries={trackedIndexEntries}",
                $"trackedHashMeta={trackedHashMeta}",
                $"trackedTotal={totalTracked}",
            ]
        );
    }

    private static string JoinSample(IEnumerable<string> values, int max = 20)
    {
        List<string> list = values.Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(value => value, StringComparer.Ordinal)
            .Take(max)
            .ToList();

        return string.Join(",", list);
    }

    private async Task ResetInternal(List<Post> posts)
    {
        PostsDbContext db = await GetDbContext();

        await using (var tx = await db.Database.BeginTransactionAsync())
        {
            try
            {
                await db.PostChangeFields.ExecuteDeleteAsync();
                await db.PostChanges.ExecuteDeleteAsync();
                await db.PostMediaVariants.ExecuteDeleteAsync();
                await db.PostMedias.ExecuteDeleteAsync();
                await db.PostHashtags.ExecuteDeleteAsync();
                await db.PostIndexEntries.ExecuteDeleteAsync();
                await db.Posts.ExecuteDeleteAsync();
                await db.Profiles.ExecuteDeleteAsync();
                await db.PostHashMeta.ExecuteDeleteAsync();

                await tx.CommitAsync();
            }
            catch
            {
                await tx.RollbackAsync();
                throw;
            }
        }

        Dictionary<string, Post> normalizedPosts = posts
            .Where(post => !string.IsNullOrWhiteSpace(post.Id))
            .GroupBy(post => post.Id, StringComparer.Ordinal)
            .Select(group => group.Last().Clone())
            .ToDictionary(post => post.Id, StringComparer.Ordinal);

        if (normalizedPosts.Count == 0)
            return;

        HashSet<string> profileIds = normalizedPosts
            .Values.Select(post => post.Profile.Id)
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .ToHashSet(StringComparer.Ordinal);

        Dictionary<string, PostProfileEntity> profilesById = await LoadProfilesByIds(
            db,
            [.. profileIds]
        );

        bool autoDetectOriginal = db.ChangeTracker.AutoDetectChangesEnabled;
        db.ChangeTracker.AutoDetectChangesEnabled = false;

        try
        {
            List<PostEntity> batch = [];

            foreach (Post post in normalizedPosts.Values)
            {
                PostProfileEntity profile = GetOrCreateProfileEntity(
                    db,
                    profilesById,
                    post.Profile
                );

                PostEntity entity = ToEntity(post, profile);
                batch.Add(entity);

                if (batch.Count < SqlInChunkSize)
                    continue;

                db.Posts.AddRange(batch);
                batch.Clear();
            }

            if (batch.Count > 0)
                db.Posts.AddRange(batch);
        }
        finally
        {
            db.ChangeTracker.AutoDetectChangesEnabled = autoDetectOriginal;
        }

        await UpsertHashMetaForPosts(db, normalizedPosts.Values);
    }

    private static PostProfileEntity GetOrCreateProfileEntity(
        PostsDbContext db,
        Dictionary<string, PostProfileEntity> profilesById,
        PostProfile profile
    )
    {
        string id = profile.Id;

        PostProfileEntity? tracked = db.Profiles.Local.FirstOrDefault(o => o.Id == id);

        if (tracked is not null)
        {
            UpdateProfileEntity(tracked, profile);
            profilesById[id] = tracked;
            return tracked;
        }

        if (profilesById.TryGetValue(id, out PostProfileEntity? existing))
        {
            if (db.Entry(existing).State == EntityState.Detached)
                db.Profiles.Attach(existing);

            UpdateProfileEntity(existing, profile);
            return existing;
        }

        PostProfileEntity created = ToProfileEntity(profile);
        profilesById[id] = created;
        db.Profiles.Add(created);

        return created;
    }

    private static void UpdateProfileEntity(PostProfileEntity entity, PostProfile profile)
    {
        entity.UserName = profile.UserName;
        entity.Name = profile.Name;
        entity.BannerUrl = profile.BannerUrl;
        entity.ImageUrl = profile.ImageUrl;
        entity.Following = profile.Following;
        entity.CountMedia = profile.Count?.Media;
    }

    private static PostEntity ToEntity(Post post, PostProfileEntity profile)
    {
        Dictionary<string, PostProfileEntity> profiles = new(StringComparer.Ordinal)
        {
            [profile.Id] = profile,
        };

        PostEntity entity = ToEntity(post, profiles);
        entity.Profile = profile;
        entity.ProfileId = profile.Id;
        return entity;
    }

    private static async Task<Dictionary<string, PostProfileEntity>> LoadProfilesByIds(
        PostsDbContext db,
        IReadOnlyCollection<string> ids
    )
    {
        if (ids.Count == 0)
            return new Dictionary<string, PostProfileEntity>(StringComparer.Ordinal);

        Dictionary<string, PostProfileEntity> result = new(StringComparer.Ordinal);

        foreach (List<string> chunk in ChunkStrings(ids))
        {
            List<PostProfileEntity> rows = await db
                .Profiles.Where(profile => chunk.Contains(profile.Id))
                .ToListAsync();

            foreach (PostProfileEntity row in rows)
                result[row.Id] = row;
        }

        return result;
    }

    private static async Task UpsertHashMetaForPosts(PostsDbContext db, IEnumerable<Post> posts)
    {
        Dictionary<string, Post> normalized = posts
            .Where(post => !string.IsNullOrWhiteSpace(post.Id))
            .GroupBy(post => post.Id, StringComparer.Ordinal)
            .Select(group => group.Last())
            .ToDictionary(post => post.Id, StringComparer.Ordinal);

        if (normalized.Count == 0)
            return;

        foreach (List<string> chunk in ChunkStrings(normalized.Keys))
        {
            Dictionary<string, PostHashMetaEntity> existing = await db
                .PostHashMeta.Where(row => chunk.Contains(row.Id))
                .ToDictionaryAsync(row => row.Id, StringComparer.Ordinal);

            foreach (string id in chunk)
            {
                Post post = normalized[id];
                string hash = Backup.App.Utils.PostHash.Compute(post);

                if (existing.TryGetValue(id, out PostHashMetaEntity? row))
                {
                    row.Hash = hash;
                    row.Deleted = post.Deleted;
                    continue;
                }

                db.PostHashMeta.Add(
                    new PostHashMetaEntity
                    {
                        Id = id,
                        Hash = hash,
                        Deleted = post.Deleted,
                    }
                );
            }
        }
    }

    private void MergeData(Post post, Post result, Change change)
    {
        result.Profile.UserName ??= post.Profile.UserName;
        result.Profile.Name ??= post.Profile.Name;
        result.Profile.BannerUrl ??= post.Profile.BannerUrl;
        result.Profile.ImageUrl ??= post.Profile.ImageUrl;
        result.Profile.Following ??= post.Profile.Following;

        if (post.Equals(result))
            return;

        change.Data = post.Clone();

        _logger.LogInformation(
            "id: {id}, userId: {userId}, userName: {userName}",
            post.Id,
            change.UserId,
            result.Profile.UserName
        );

        Backup.Infrastructure.Logging.LoggingExtensions.LogAsJsonDiff(
            _logger,
            "old data",
            "new data",
            post.Clone(),
            result.Clone()
        );
    }

    private void MergeIndex(Post post, Post result, Change change, string origin)
    {
        if (!post.Index.ContainsKey(change.UserId))
            post.Index[change.UserId] = [];

        if (!post.Index[change.UserId].TryGetValue(origin, out IndexData? indexPost))
        {
            indexPost = new();
            post.Index[change.UserId][origin] = indexPost;
        }

        IndexData indexResult = result.Index[change.UserId][origin];

        if (
            indexPost.Previous is null
            || indexPost.Next is null
            || indexResult.Previous is null
            || indexResult.Next is null
            || indexPost.Equals(indexResult)
        )
            return;

        change.Index = post.CloneIndex()[change.UserId];

        _logger.LogInformation(
            "id: {id}, userId: {userId}, userName: {userName}",
            post.Id,
            change.UserId,
            result.Profile.UserName
        );

        Backup.Infrastructure.Logging.LoggingExtensions.LogAsJsonDiff(
            _logger,
            "old index",
            "new index",
            post.Index[change.UserId],
            result.Index[change.UserId]
        );
    }
}

