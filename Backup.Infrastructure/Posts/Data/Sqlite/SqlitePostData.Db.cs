using Backup.Infrastructure.Models.Config.Data;
using Backup.Infrastructure.Posts.Adapters;
using Backup.Infrastructure.Posts.Models.Stored;
using Backup.Infrastructure.Utils;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Backup.Infrastructure.Posts.Data.Sqlite;

public partial class SqlitePostData
{
    private const int HashRebuildBatchSize = 500;
    private static readonly string[] CommonPragmas =
    [
        "PRAGMA busy_timeout = 30000;",
        "PRAGMA foreign_keys = ON;",
    ];

    private static readonly string[] LocalDiskPragmas =
    [
        "PRAGMA journal_mode = WAL;",
        "PRAGMA synchronous = NORMAL;",
        "PRAGMA temp_store = MEMORY;",
        "PRAGMA cache_size = -32768;",
        "PRAGMA mmap_size = 268435456;",
    ];

    private async Task EnsureSchema()
    {
        string dbPath = GetDatabasePath();
        Directory.CreateDirectory(GetBasePath(_partition.GetPrimary()));
        const int maxAttempts = 8;

        for (int attempt = 1; attempt <= maxAttempts; attempt++)
        {
            try
            {
                await using PostsDbContext db = CreateDbContext(dbPath);
                await ApplyConnectionPragmas(db);
                await db.Database.EnsureCreatedAsync();
                await db.Database.ExecuteSqlRawAsync(
                    """
                    CREATE TABLE IF NOT EXISTS post_meta (
                        id TEXT NOT NULL PRIMARY KEY,
                        hash TEXT NOT NULL,
                        deleted INTEGER NOT NULL DEFAULT 0
                    );
                    """
                );
                await db.Database.ExecuteSqlRawAsync(
                    """
                    CREATE INDEX IF NOT EXISTS ix_post_index_entries_user_id_origin_post_id
                    ON post_index_entries (user_id, origin, post_id);
                    """
                );
                await db.Database.ExecuteSqlRawAsync("PRAGMA optimize;");

                return;
            }
            catch (SqliteException ex) when (IsTransientLock(ex) && attempt < maxAttempts)
            {
                int delayMs = attempt * 500;

                _logger.LogWarning(
                    "sqlite setup lock on '{dbPath}' attempt {attempt}/{maxAttempts}; retrying in {delayMs}ms",
                    dbPath,
                    attempt,
                    maxAttempts,
                    delayMs
                );

                await Task.Delay(delayMs);
            }
        }
    }

    private static bool IsTransientLock(SqliteException ex) => ex.SqliteErrorCode is 5 or 6;

    private static DateTime NormalizeUtc(DateTime value) =>
        value.Kind switch
        {
            DateTimeKind.Utc => value,
            DateTimeKind.Local => value.ToUniversalTime(),
            _ => DateTime.SpecifyKind(value, DateTimeKind.Utc),
        };

    private async Task RebuildHashMetadata()
    {
        await using PostsDbContext db = CreateDbContext(GetDatabasePath());
        await ApplyConnectionPragmas(db);
        int rebuilt = 0;

        bool requiresRebuild = await db.Posts.AnyAsync(post =>
            !db.PostHashMeta.Any(meta => meta.Id == post.Id && meta.Hash.StartsWith("v2:"))
        );

        if (requiresRebuild)
            _logger.LogInformation("sqlite post hash metadata v2 rebuild started");

        while (true)
        {
            List<string> ids = await db
                .Posts.AsNoTracking()
                .Where(post =>
                    !db.PostHashMeta.Any(meta => meta.Id == post.Id && meta.Hash.StartsWith("v2:"))
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
                bool deleted =
                    metadata.TryGetValue(entity.Id, out PostHashMetaEntity? meta) && meta.Deleted;
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
                    "sqlite post hash metadata v2 rebuild progress: posts={postCount}",
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
                "sqlite post hash metadata v2 rebuild completed: posts={postCount}",
                rebuilt
            );
    }

    private static async Task ApplyConnectionPragmas(PostsDbContext db)
    {
        foreach (string pragma in CommonPragmas)
            await db.Database.ExecuteSqlRawAsync(pragma);

        if (!ShouldUseLocalDiskPragmas())
            return;

        foreach (string pragma in LocalDiskPragmas)
            await db.Database.ExecuteSqlRawAsync(pragma);
    }

    private static bool ShouldUseLocalDiskPragmas()
    {
        string? configured = Environment.GetEnvironmentVariable(
            "BACKUP__POSTS__SQLITE__LOCAL_DISK"
        );

        if (bool.TryParse(configured, out bool localDiskConfigured))
            return localDiskConfigured;

        return true;
    }

    private string GetBasePath(PartitionConfig p) =>
        UtilsPath.GetPath([.. p.Paths, .. _config.Paths.Paths, .. _config.Paths.Post.Paths]);

    private string GetDatabasePath(PartitionConfig? p = null)
    {
        string? file = _config.Paths.Post.File;

        if (string.IsNullOrWhiteSpace(file))
            throw new InvalidOperationException("Post sqlite file is not configured.");

        PartitionConfig primary = p ?? _partition.GetPrimary();
        return Path.Combine(GetBasePath(primary), file);
    }

    private async Task Replicate()
    {
        PartitionConfig primary = _partition.GetPrimary();
        string mainPath = GetDatabasePath(primary);

        List<PartitionConfig> replicas = _partition
            .GetPartitions()
            .Where(o => o.Id != primary.Id)
            .ToList();

        foreach (PartitionConfig replica in replicas)
        {
            string replicaPath = GetDatabasePath(replica);
            Directory.CreateDirectory(GetBasePath(replica));
            const int maxAttempts = 3;

            for (int attempt = 1; attempt <= maxAttempts; attempt++)
            {
                try
                {
                    using SqliteConnection source = CreateSqliteConnection(
                        mainPath,
                        SqliteOpenMode.ReadOnly
                    );

                    using SqliteConnection destination = CreateSqliteConnection(
                        replicaPath,
                        SqliteOpenMode.ReadWriteCreate
                    );

                    await source.OpenAsync();
                    await destination.OpenAsync();
                    source.BackupDatabase(destination);
                    break;
                }
                catch (SqliteException ex) when (IsTransientLock(ex) && attempt < maxAttempts)
                {
                    int delayMs = attempt * 500;

                    _logger.LogWarning(
                        "sqlite replicate lock from '{source}' to '{target}' attempt {attempt}/{maxAttempts}; retrying in {delayMs}ms",
                        mainPath,
                        replicaPath,
                        attempt,
                        maxAttempts,
                        delayMs
                    );

                    await Task.Delay(delayMs);
                }
            }
        }
    }

    private static PostsDbContext CreateDbContext(string dbPath) => new(dbPath);

    private static SqliteConnection CreateSqliteConnection(string dbPath, SqliteOpenMode mode)
    {
        SqliteConnectionStringBuilder connection = new()
        {
            DataSource = dbPath,
            Mode = mode,
            Cache = SqliteCacheMode.Shared,
            DefaultTimeout = 30,
        };

        return new SqliteConnection(connection.ToString());
    }
}
