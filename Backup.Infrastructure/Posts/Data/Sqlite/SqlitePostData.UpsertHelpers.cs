using Backup.Application.Posts;
using Backup.Infrastructure.Posts.Adapters;
using Backup.Infrastructure.Posts.Models.Stored;
using Microsoft.EntityFrameworkCore;

namespace Backup.Infrastructure.Posts.Data.Sqlite;

public partial class SqlitePostData
{
    private async Task<Dictionary<string, PostProfileEntity>> LoadProfilesForPosts(
        PostsDbContext db,
        IEnumerable<Post> posts
    )
    {
        HashSet<string> profileIds = posts
            .Select(post => post.Profile.Id)
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .ToHashSet(StringComparer.Ordinal);

        return await LoadProfilesByIds(db, [.. profileIds]);
    }

    private void BufferPostEntity(
        PostsDbContext db,
        Dictionary<string, PostProfileEntity> profilesById,
        List<PostEntity> batch,
        Post post
    )
    {
        PostProfileEntity profileEntity = GetOrCreateProfileEntity(db, profilesById, post.Profile);
        PostEntity created = ToEntity(post, profileEntity, _postChangeComputationService);
        batch.Add(created);

        if (batch.Count < SqlInChunkSize)
            return;

        db.Posts.AddRange(batch);
        batch.Clear();
    }

    private static void FlushPostBatch(PostsDbContext db, List<PostEntity> batch)
    {
        if (batch.Count == 0)
            return;

        db.Posts.AddRange(batch);
        batch.Clear();
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

    private static PostEntity ToEntity(
        Post post,
        PostProfileEntity profile,
        IPostChangeComputationService postChangeComputationService
    )
    {
        Dictionary<string, PostProfileEntity> profiles = new(StringComparer.Ordinal)
        {
            [profile.Id] = profile,
        };

        PostEntity entity = ToEntity(post, profiles, postChangeComputationService);
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

    private async Task UpsertHashMetaForPosts(PostsDbContext db, IEnumerable<Post> posts)
    {
        IReadOnlyList<Post> normalizedPosts = NormalizePosts(posts.ToList());
        Dictionary<string, Post> normalized = normalizedPosts.ToDictionary(
            post => post.Id,
            StringComparer.Ordinal
        );

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
                Backup.Domain.Posts.Post domainPost = PostReplicationMapper.ToDomain(post);
                string hash = _postHashingService.Compute(domainPost);

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
}
