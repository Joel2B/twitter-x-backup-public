using Backup.Infrastructure.Media.Abstractions.Services;
using Backup.Infrastructure.Media.Models;
using Microsoft.Data.Sqlite;

namespace Backup.Infrastructure.Media.Data;

public sealed class SqliteMediaCachePersistenceIOService : IMediaCachePersistenceIOService
{
    private const string PrimaryTableName = "media_cache_primary_entries";
    private const string IncrementalTableName = "media_cache_incremental_entries";

    public Task<bool> PrimarySnapshotExists(
        string file,
        CancellationToken cancellationToken = default
    ) => Task.FromResult(File.Exists(file));

    public async Task<IReadOnlyList<MediaCacheEntry>> LoadIncrementalSnapshots(
        string directory,
        CancellationToken cancellationToken = default
    )
    {
        string databasePath = GetIncrementalDatabasePath(directory);

        if (!File.Exists(databasePath))
            return [];

        await using SqliteConnection connection = OpenConnection(databasePath);
        await EnsureTable(connection, IncrementalTableName, cancellationToken);

        SqliteCommand command = connection.CreateCommand();
        command.CommandText = $"""
            SELECT path, stream_size, file_size, partition_id
            FROM {IncrementalTableName}
            ORDER BY file_name
            """;

        return await ReadEntries(command, cancellationToken);
    }

    public async Task<IReadOnlyList<MediaCacheEntry>> LoadPrimarySnapshot(
        string file,
        CancellationToken cancellationToken = default
    )
    {
        if (!File.Exists(file))
            return [];

        await using SqliteConnection connection = OpenConnection(file);
        await EnsureTable(connection, PrimaryTableName, cancellationToken);

        SqliteCommand command = connection.CreateCommand();
        command.CommandText = $"""
            SELECT path, stream_size, file_size, partition_id
            FROM {PrimaryTableName}
            ORDER BY path
            """;

        return await ReadEntries(command, cancellationToken);
    }

    public async Task SavePrimarySnapshot(
        string file,
        IReadOnlyCollection<MediaCacheEntry> entries,
        CancellationToken cancellationToken = default
    )
    {
        EnsureDirectory(file);

        await using SqliteConnection connection = OpenConnection(file);
        await EnsureTable(connection, PrimaryTableName, cancellationToken);
        await using SqliteTransaction transaction = (SqliteTransaction)
            await connection.BeginTransactionAsync(cancellationToken);

        SqliteCommand delete = connection.CreateCommand();
        delete.Transaction = transaction;
        delete.CommandText = $"DELETE FROM {PrimaryTableName}";
        await delete.ExecuteNonQueryAsync(cancellationToken);

        foreach (MediaCacheEntry entry in entries)
        {
            await UpsertEntry(
                connection,
                transaction,
                PrimaryTableName,
                entry,
                null,
                cancellationToken
            );
        }

        await transaction.CommitAsync(cancellationToken);
    }

    public async Task SaveIncrementalSnapshot(
        string directory,
        MediaCacheEntry entry,
        string fileName,
        CancellationToken cancellationToken = default
    )
    {
        string databasePath = GetIncrementalDatabasePath(directory);
        EnsureDirectory(databasePath);

        await using SqliteConnection connection = OpenConnection(databasePath);
        await EnsureTable(connection, IncrementalTableName, cancellationToken);
        await UpsertEntry(
            connection,
            transaction: null,
            IncrementalTableName,
            entry,
            fileName,
            cancellationToken
        );
    }

    public Task ReplicatePrimarySnapshot(
        string primaryFilePath,
        IReadOnlyCollection<string> replicaPaths,
        CancellationToken cancellationToken = default
    )
    {
        if (!File.Exists(primaryFilePath))
            return Task.CompletedTask;

        foreach (string path in replicaPaths)
        {
            cancellationToken.ThrowIfCancellationRequested();
            EnsureDirectory(path);

            if (File.Exists(path))
                File.Delete(path);

            File.Copy(primaryFilePath, path);
        }

        return Task.CompletedTask;
    }

    public void ResetIncrementalSnapshotDirectory(string directory)
    {
        string databasePath = GetIncrementalDatabasePath(directory);

        if (File.Exists(databasePath))
            File.Delete(databasePath);

        Directory.CreateDirectory(directory);
    }

    private static async Task EnsureTable(
        SqliteConnection connection,
        string tableName,
        CancellationToken cancellationToken
    )
    {
        SqliteCommand command = connection.CreateCommand();
        command.CommandText =
            tableName == PrimaryTableName
                ? $"""
                    CREATE TABLE IF NOT EXISTS {PrimaryTableName} (
                        path TEXT PRIMARY KEY,
                        stream_size INTEGER NULL,
                        file_size INTEGER NULL,
                        partition_id INTEGER NULL
                    );
                    """
                : $"""
                    CREATE TABLE IF NOT EXISTS {IncrementalTableName} (
                        file_name TEXT PRIMARY KEY,
                        path TEXT NOT NULL,
                        stream_size INTEGER NULL,
                        file_size INTEGER NULL,
                        partition_id INTEGER NULL
                    );
                    """;

        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task<List<MediaCacheEntry>> ReadEntries(
        SqliteCommand command,
        CancellationToken cancellationToken
    )
    {
        List<MediaCacheEntry> entries = [];
        await using SqliteDataReader reader = await command.ExecuteReaderAsync(cancellationToken);

        while (await reader.ReadAsync(cancellationToken))
        {
            entries.Add(
                new MediaCacheEntry
                {
                    Path = reader.GetString(0),
                    Size = new MediaCacheSize
                    {
                        Stream = reader.IsDBNull(1) ? null : reader.GetInt64(1),
                        File = reader.IsDBNull(2) ? null : reader.GetInt64(2),
                    },
                    PartitionId = reader.IsDBNull(3) ? null : reader.GetInt32(3),
                }
            );
        }

        return entries;
    }

    private static async Task UpsertEntry(
        SqliteConnection connection,
        SqliteTransaction? transaction,
        string tableName,
        MediaCacheEntry entry,
        string? fileName,
        CancellationToken cancellationToken
    )
    {
        SqliteCommand command = connection.CreateCommand();
        command.Transaction = transaction;

        if (tableName == PrimaryTableName)
        {
            command.CommandText = $"""
                INSERT INTO {PrimaryTableName} (path, stream_size, file_size, partition_id)
                VALUES ($path, $streamSize, $fileSize, $partitionId)
                ON CONFLICT(path) DO UPDATE SET
                    stream_size = excluded.stream_size,
                    file_size = excluded.file_size,
                    partition_id = excluded.partition_id;
                """;
        }
        else
        {
            command.CommandText = $"""
                INSERT INTO {IncrementalTableName} (file_name, path, stream_size, file_size, partition_id)
                VALUES ($fileName, $path, $streamSize, $fileSize, $partitionId)
                ON CONFLICT(file_name) DO UPDATE SET
                    path = excluded.path,
                    stream_size = excluded.stream_size,
                    file_size = excluded.file_size,
                    partition_id = excluded.partition_id;
                """;
            command.Parameters.AddWithValue("$fileName", fileName ?? string.Empty);
        }

        command.Parameters.AddWithValue("$path", entry.Path);
        command.Parameters.AddWithValue("$streamSize", (object?)entry.Size?.Stream ?? DBNull.Value);
        command.Parameters.AddWithValue("$fileSize", (object?)entry.Size?.File ?? DBNull.Value);
        command.Parameters.AddWithValue("$partitionId", (object?)entry.PartitionId ?? DBNull.Value);

        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static SqliteConnection OpenConnection(string databasePath)
    {
        SqliteConnectionStringBuilder builder = new()
        {
            DataSource = databasePath,
            Mode = SqliteOpenMode.ReadWriteCreate,
            Pooling = false,
        };

        SqliteConnection connection = new(builder.ToString());
        connection.Open();
        return connection;
    }

    private static string GetIncrementalDatabasePath(string directory) =>
        Path.Combine(directory, "media-cache-incremental.sqlite");

    private static void EnsureDirectory(string path)
    {
        string? directory = Path.GetDirectoryName(path);

        if (!string.IsNullOrWhiteSpace(directory))
            Directory.CreateDirectory(directory);
    }
}
