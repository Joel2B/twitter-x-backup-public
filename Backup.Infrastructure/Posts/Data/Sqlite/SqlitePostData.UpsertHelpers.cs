using Backup.Infrastructure.Posts.Models;
using Microsoft.EntityFrameworkCore;

namespace Backup.Infrastructure.Posts.Data.Sqlite;

public partial class SqlitePostData
{
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
                string hash = Backup.Infrastructure.Utils.PostHash.Compute(post);

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
