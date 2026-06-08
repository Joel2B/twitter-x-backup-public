using Backup.Infrastructure.Media.Abstractions.Services;
using Backup.Infrastructure.Media.Models;
using Backup.Infrastructure.Models.Config.Data.Media;
using Npgsql;

namespace Backup.Infrastructure.Media.Data;

public sealed class PostgresMediaCachePersistenceIOService(
    string storeId,
    MediaCacheConfig cacheConfig
) : IMediaCachePersistenceIOService
{
    private const string PrimaryTableName = "media_cache_primary_entries";
    private const string IncrementalTableName = "media_cache_incremental_entries";

    private readonly string _connectionString = GetConnectionString(storeId, cacheConfig);
    private readonly string _storeNamespace = GetStoreNamespace(storeId, cacheConfig);
    private bool _schemaEnsured;

    public async Task<bool> PrimarySnapshotExists(
        string file,
        CancellationToken cancellationToken = default
    )
    {
        string cacheNamespace = BuildPrimaryNamespace(file);

        await using NpgsqlConnection connection = await OpenConnection(cancellationToken);
        await EnsureSchema(connection, cancellationToken);

        await using NpgsqlCommand command = connection.CreateCommand();
        command.CommandText = $"""
            SELECT EXISTS(
                SELECT 1
                FROM {PrimaryTableName}
                WHERE namespace = @namespace
            )
            """;
        command.Parameters.AddWithValue("namespace", cacheNamespace);

        object? result = await command.ExecuteScalarAsync(cancellationToken);
        return result is true;
    }

    public async Task<IReadOnlyList<MediaCacheEntry>> LoadIncrementalSnapshots(
        string directory,
        CancellationToken cancellationToken = default
    )
    {
        string cacheNamespace = BuildIncrementalNamespace(directory);

        await using NpgsqlConnection connection = await OpenConnection(cancellationToken);
        await EnsureSchema(connection, cancellationToken);

        await using NpgsqlCommand command = connection.CreateCommand();
        command.CommandText = $"""
            SELECT path, stream_size, file_size, partition_id
            FROM {IncrementalTableName}
            WHERE namespace = @namespace
            ORDER BY file_name
            """;
        command.Parameters.AddWithValue("namespace", cacheNamespace);

        return await ReadEntries(command, cancellationToken);
    }

    public async Task<IReadOnlyList<MediaCacheEntry>> LoadPrimarySnapshot(
        string file,
        CancellationToken cancellationToken = default
    )
    {
        string cacheNamespace = BuildPrimaryNamespace(file);

        await using NpgsqlConnection connection = await OpenConnection(cancellationToken);
        await EnsureSchema(connection, cancellationToken);

        await using NpgsqlCommand command = connection.CreateCommand();
        command.CommandText = $"""
            SELECT path, stream_size, file_size, partition_id
            FROM {PrimaryTableName}
            WHERE namespace = @namespace
            ORDER BY path
            """;
        command.Parameters.AddWithValue("namespace", cacheNamespace);

        return await ReadEntries(command, cancellationToken);
    }

    public async Task SavePrimarySnapshot(
        string file,
        IReadOnlyCollection<MediaCacheEntry> entries,
        CancellationToken cancellationToken = default
    )
    {
        string cacheNamespace = BuildPrimaryNamespace(file);

        await using NpgsqlConnection connection = await OpenConnection(cancellationToken);
        await EnsureSchema(connection, cancellationToken);
        await using NpgsqlTransaction transaction = await connection.BeginTransactionAsync(
            cancellationToken
        );

        await using (NpgsqlCommand delete = connection.CreateCommand())
        {
            delete.Transaction = transaction;
            delete.CommandText = $"DELETE FROM {PrimaryTableName} WHERE namespace = @namespace";
            delete.Parameters.AddWithValue("namespace", cacheNamespace);
            await delete.ExecuteNonQueryAsync(cancellationToken);
        }

        foreach (MediaCacheEntry entry in entries)
            await UpsertPrimaryEntry(
                connection,
                transaction,
                cacheNamespace,
                entry,
                cancellationToken
            );

        await transaction.CommitAsync(cancellationToken);
    }

    public async Task SaveIncrementalSnapshot(
        string directory,
        MediaCacheEntry entry,
        string fileName,
        CancellationToken cancellationToken = default
    )
    {
        string cacheNamespace = BuildIncrementalNamespace(directory);

        await using NpgsqlConnection connection = await OpenConnection(cancellationToken);
        await EnsureSchema(connection, cancellationToken);
        await UpsertIncrementalEntry(
            connection,
            cacheNamespace,
            entry,
            fileName,
            cancellationToken
        );
    }

    public Task ReplicatePrimarySnapshot(
        string primaryFilePath,
        IReadOnlyCollection<string> replicaPaths,
        CancellationToken cancellationToken = default
    ) => Task.CompletedTask;

    public void ResetIncrementalSnapshotDirectory(string directory)
    {
        string cacheNamespace = BuildIncrementalNamespace(directory);

        using NpgsqlConnection connection = new(_connectionString);
        connection.Open();
        EnsureSchema(connection, CancellationToken.None).GetAwaiter().GetResult();

        using NpgsqlCommand command = connection.CreateCommand();
        command.CommandText = $"DELETE FROM {IncrementalTableName} WHERE namespace = @namespace";
        command.Parameters.AddWithValue("namespace", cacheNamespace);
        command.ExecuteNonQuery();
    }

    private async Task EnsureSchema(
        NpgsqlConnection connection,
        CancellationToken cancellationToken
    )
    {
        if (_schemaEnsured)
            return;

        await using NpgsqlCommand command = connection.CreateCommand();
        command.CommandText = $"""
            CREATE TABLE IF NOT EXISTS {PrimaryTableName} (
                namespace TEXT NOT NULL,
                path TEXT NOT NULL,
                stream_size BIGINT NULL,
                file_size BIGINT NULL,
                partition_id INTEGER NULL,
                PRIMARY KEY (namespace, path)
            );

            CREATE TABLE IF NOT EXISTS {IncrementalTableName} (
                namespace TEXT NOT NULL,
                file_name TEXT NOT NULL,
                path TEXT NOT NULL,
                stream_size BIGINT NULL,
                file_size BIGINT NULL,
                partition_id INTEGER NULL,
                PRIMARY KEY (namespace, file_name)
            );
            """;

        await command.ExecuteNonQueryAsync(cancellationToken);
        _schemaEnsured = true;
    }

    private static async Task<List<MediaCacheEntry>> ReadEntries(
        NpgsqlCommand command,
        CancellationToken cancellationToken
    )
    {
        List<MediaCacheEntry> entries = [];
        await using NpgsqlDataReader reader = await command.ExecuteReaderAsync(cancellationToken);

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

    private static async Task UpsertPrimaryEntry(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        string cacheNamespace,
        MediaCacheEntry entry,
        CancellationToken cancellationToken
    )
    {
        await using NpgsqlCommand command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = $"""
            INSERT INTO {PrimaryTableName} (namespace, path, stream_size, file_size, partition_id)
            VALUES (@namespace, @path, @streamSize, @fileSize, @partitionId)
            ON CONFLICT(namespace, path) DO UPDATE SET
                stream_size = EXCLUDED.stream_size,
                file_size = EXCLUDED.file_size,
                partition_id = EXCLUDED.partition_id
            """;
        AddCommonParameters(command, cacheNamespace, entry);

        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task UpsertIncrementalEntry(
        NpgsqlConnection connection,
        string cacheNamespace,
        MediaCacheEntry entry,
        string fileName,
        CancellationToken cancellationToken
    )
    {
        await using NpgsqlCommand command = connection.CreateCommand();
        command.CommandText = $"""
            INSERT INTO {IncrementalTableName} (
                namespace,
                file_name,
                path,
                stream_size,
                file_size,
                partition_id
            )
            VALUES (@namespace, @fileName, @path, @streamSize, @fileSize, @partitionId)
            ON CONFLICT(namespace, file_name) DO UPDATE SET
                path = EXCLUDED.path,
                stream_size = EXCLUDED.stream_size,
                file_size = EXCLUDED.file_size,
                partition_id = EXCLUDED.partition_id
            """;
        command.Parameters.AddWithValue("fileName", fileName);
        AddCommonParameters(command, cacheNamespace, entry);

        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static void AddCommonParameters(
        NpgsqlCommand command,
        string cacheNamespace,
        MediaCacheEntry entry
    )
    {
        command.Parameters.AddWithValue("namespace", cacheNamespace);
        command.Parameters.AddWithValue("path", entry.Path);
        command.Parameters.AddWithValue("streamSize", (object?)entry.Size?.Stream ?? DBNull.Value);
        command.Parameters.AddWithValue("fileSize", (object?)entry.Size?.File ?? DBNull.Value);
        command.Parameters.AddWithValue("partitionId", (object?)entry.PartitionId ?? DBNull.Value);
    }

    private async Task<NpgsqlConnection> OpenConnection(CancellationToken cancellationToken)
    {
        NpgsqlConnection connection = new(_connectionString);
        await connection.OpenAsync(cancellationToken);
        return connection;
    }

    private string BuildPrimaryNamespace(string file) =>
        $"{_storeNamespace}|primary|{NormalizeLocator(file)}";

    private string BuildIncrementalNamespace(string directory) =>
        $"{_storeNamespace}|incremental|{NormalizeLocator(directory)}";

    private static string NormalizeLocator(string value) =>
        value.Trim().Replace('\\', '/').ToLowerInvariant();

    private static string GetConnectionString(string storeId, MediaCacheConfig cacheConfig)
    {
        string? connectionString = cacheConfig.ConnectionString;

        if (!string.IsNullOrWhiteSpace(connectionString))
            return connectionString;

        throw new InvalidOperationException(
            $"Media cache postgres connection string is not configured for store '{storeId}'."
        );
    }

    private static string GetStoreNamespace(string storeId, MediaCacheConfig cacheConfig) =>
        $"backup:media-cache:{storeId}:{cacheConfig.Id ?? cacheConfig.Type}";
}
