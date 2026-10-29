using Backup.Infrastructure.Models.Config.Data;
using Backup.Infrastructure.Posts.Adapters;
using Backup.Infrastructure.Posts.Models.Stored;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Backup.Infrastructure.Posts.Data.Postgres;

public partial class PostgresPostData
{
    private const int HashRebuildBatchSize = 500;

    private async Task EnsureSchema()
    {
        string connectionString = GetConnectionString();
        await using PostsDbContext db = CreateDbContext(connectionString);
        await db.Database.EnsureCreatedAsync();
    }

    private string GetConnectionString()
    {
        string? connectionString = _config.ConnectionString;

        if (!string.IsNullOrWhiteSpace(connectionString))
            return connectionString;

        throw new InvalidOperationException(
            $"Post postgres connection string is not configured for store '{Id ?? _config.Id ?? "unknown"}'."
        );
    }

    private static DateTime NormalizeUtc(DateTime value) =>
        value.Kind switch
        {
            DateTimeKind.Utc => value,
            DateTimeKind.Local => value.ToUniversalTime(),
            _ => DateTime.SpecifyKind(value, DateTimeKind.Utc),
        };

    private async Task RebuildHashMetadata()
    {
        await using PostsDbContext db = CreateDbContext(GetConnectionString());
        int rebuilt = 0;

        bool requiresRebuild = await db.Posts.AnyAsync(post =>
            !db.PostHashMeta.Any(meta => meta.Id == post.Id && meta.Hash.StartsWith("v2:"))
        );

        if (requiresRebuild)
            _logger.LogInformation("postgres post hash metadata v2 rebuild started");

        while (true)
        {
            List<string> ids = await db
                .Posts.AsNoTracking()
                .Where(post =>
                    !db.PostHashMeta.Any(meta =>
                        meta.Id == post.Id && meta.Hash.StartsWith("v2:")
                    )
                )
                .OrderBy(post => post.Id)
                .Select(post => post.Id)
                .Take(HashRebuildBatchSize)
                .ToListAsync();

            if (ids.Count == 0)
                break;

            await using var tx = await db.Database.BeginTransactionAsync();
            List<PostEntity> entities = await LoadPostGraphByIds(db, ids);
            Dictionary<string, PostHashMetaEntity> metadata = await db
                .PostHashMeta.Where(meta => ids.Contains(meta.Id))
                .ToDictionaryAsync(meta => meta.Id, StringComparer.Ordinal);

            foreach (PostEntity entity in entities)
            {
                bool deleted = metadata.TryGetValue(entity.Id, out PostHashMetaEntity? meta)
                    && meta.Deleted;
                Post post = ToModel(entity, deleted);
                string hash = _postHashingService.Compute(PostReplicationMapper.ToDomain(post));

                if (meta is null)
                {
                    meta = new PostHashMetaEntity
                    {
                        Id = entity.Id,
                        Hash = hash,
                        Deleted = false,
                    };
                    db.PostHashMeta.Add(meta);
                }

                meta.Hash = hash;
            }

            await db.SaveChangesAsync();
            await tx.CommitAsync();
            db.ChangeTracker.Clear();
            rebuilt += ids.Count;

            if (rebuilt % 10000 == 0)
                _logger.LogInformation(
                    "postgres post hash metadata v2 rebuild progress: posts={postCount}",
                    rebuilt
                );
        }

        while (true)
        {
            List<string> orphanIds = await db
                .PostHashMeta.AsNoTracking()
                .Where(meta => !db.Posts.Any(post => post.Id == meta.Id))
                .OrderBy(meta => meta.Id)
                .Select(meta => meta.Id)
                .Take(HashRebuildBatchSize)
                .ToListAsync();

            if (orphanIds.Count == 0)
                break;

            await using var tx = await db.Database.BeginTransactionAsync();
            await db.PostHashMeta.Where(meta => orphanIds.Contains(meta.Id)).ExecuteDeleteAsync();
            await tx.CommitAsync();
        }

        if (requiresRebuild)
            _logger.LogInformation(
                "postgres post hash metadata v2 rebuild completed: posts={postCount}",
                rebuilt
            );
    }

    private static PostsDbContext CreateDbContext(string connectionString) => new(connectionString);
}
