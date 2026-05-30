using Backup.Infrastructure.Models.Config.Data;
using Backup.Infrastructure.Utils;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Backup.Infrastructure.Posts.Data;

public partial class SqlitePostData
{
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
                await db.Database.ExecuteSqlRawAsync("PRAGMA busy_timeout = 30000;");
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


