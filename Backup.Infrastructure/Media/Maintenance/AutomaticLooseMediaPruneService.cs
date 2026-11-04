using System.Data;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Backup.Application.Media.Ports;
using Backup.Infrastructure.Models.Config;
using Npgsql;
using NpgsqlTypes;

namespace Backup.Infrastructure.Media.Maintenance;

public sealed class AutomaticLooseMediaPruneService(
    AppConfig appConfig,
    IMediaOrchestrationCommand mediaCommand
)
{
    private const int RetentionDays = 7;
    private const string QuarantineDirectory = ".media-prune-quarantine";
    private static readonly Regex SupportedPath = new(
        @"^(?:[^/]+/photo/[^/]+/[^/]+/(?:jpg|png)/(?:dimension|size)/[^/]+\.(?:jpg|png)|[^/]+/(?:gif|video)/[^/]+/thumb/[^/]+/(?:jpg|png)/(?:dimension|size)/[^/]+\.(?:jpg|png)|[^/]+/video/[^/]+/mp4/[^/]+/[^/]+\.mp4)$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase
    );
    private readonly AppConfig _appConfig = appConfig;
    private readonly IMediaOrchestrationCommand _mediaCommand = mediaCommand;

    public async Task<AutomaticLooseMediaPruneResult> PruneAsync(
        string dataConfigPath,
        string mediaStoreId,
        string cacheId,
        bool runtimeStoppedConfirmed,
        int batchSize = 1000,
        IProgress<AutomaticLooseMediaPruneProgress>? progress = null,
        CancellationToken token = default
    )
    {
        if (!runtimeStoppedConfirmed)
            throw new InvalidOperationException("Runtime stopped confirmation is required.");
        if (!_appConfig.Medias.Enabled)
            throw new InvalidOperationException("Media processing is disabled; prune will not treat that as an empty policy.");
        if (batchSize <= 0)
            throw new ArgumentOutOfRangeException(nameof(batchSize));

        ResolvedConfig config = VideoVariantLooseCleanupPreviewService.LoadConfig(
            dataConfigPath,
            mediaStoreId,
            cacheId
        );
        string policyHash = Hash(JsonSerializer.SerializeToUtf8Bytes(_appConfig.Medias));

        await using NpgsqlConnection connection = new(config.ConnectionString);
        await connection.OpenAsync(token);
        await EnsureSchemaAsync(connection, token);
        await AcquireLockAsync(connection, mediaStoreId, cacheId, token);
        try
        {
            AutomaticLooseMediaPruneRun? active = await LoadActiveRunAsync(
                connection,
                mediaStoreId,
                cacheId,
                token
            );
            if (active is not null)
            {
                if (!active.PolicyHash.Equals(policyHash, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException(
                        $"Active prune run {active.RunId} uses a different Medias.json policy. Resume with the original policy before creating another run."
                    );
                return await ApplyAsync(connection, config, active, batchSize, progress, token);
            }

            IReadOnlyList<Backup.Domain.Posts.MediaInput> inputs = await _mediaCommand.GetMediaInputs(token);
            Backup.Application.Media.Models.MediaProcessingResult projection = await _mediaCommand.Process(
                inputs,
                token
            );
            HashSet<string> desired = projection
                .Filtered.SelectMany(download => download.Data)
                .Select(data => Normalize(data.Path))
                .Where(path => SupportedPath.IsMatch(path))
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            if (desired.Count == 0)
                throw new InvalidOperationException(
                    "Active policy projected zero supported loose media paths; destructive empty-policy prune is refused."
                );

            string cacheNamespace = await DiscoverNamespaceAsync(
                connection,
                mediaStoreId,
                cacheId,
                token
            );
            List<CacheRow> rows = await LoadSupportedRowsAsync(connection, cacheNamespace, token);
            AutomaticLooseMediaPruneRun run = await PlanAsync(
                connection,
                config,
                cacheNamespace,
                policyHash,
                desired,
                rows,
                token
            );
            progress?.Report(new("planned", 0, run.OperationCount, 0));
            return await ApplyAsync(connection, config, run, batchSize, progress, token);
        }
        finally
        {
            await ReleaseLockAsync(connection, mediaStoreId, cacheId, CancellationToken.None);
        }
    }

    public async Task<IReadOnlyList<AutomaticLooseMediaPruneStatus>> StatusAsync(
        string dataConfigPath,
        string mediaStoreId,
        string cacheId,
        CancellationToken token = default
    )
    {
        ResolvedConfig config = VideoVariantLooseCleanupPreviewService.LoadConfig(dataConfigPath, mediaStoreId, cacheId);
        await using NpgsqlConnection connection = new(config.ConnectionString);
        await connection.OpenAsync(token);
        await EnsureSchemaAsync(connection, token);
        await using NpgsqlCommand command = connection.CreateCommand();
        command.CommandText = """
            SELECT r.run_id, r.state, r.created_utc, r.purge_after_utc, r.operation_count,
                   count(o.*) FILTER (WHERE o.state IN ('verified', 'purged')),
                   coalesce(sum(o.file_size) FILTER (WHERE o.state IN ('verified', 'purged')), 0)
            FROM media_loose_prune_runs r
            LEFT JOIN media_loose_prune_operations o ON o.run_id = r.run_id
            WHERE r.media_store_id = @store AND r.cache_id = @cache
            GROUP BY r.run_id
            ORDER BY r.created_utc DESC
            LIMIT 20
            """;
        command.Parameters.AddWithValue("store", mediaStoreId);
        command.Parameters.AddWithValue("cache", cacheId);
        List<AutomaticLooseMediaPruneStatus> result = [];
        await using NpgsqlDataReader reader = await command.ExecuteReaderAsync(token);
        while (await reader.ReadAsync(token))
        {
            result.Add(new(
                reader.GetGuid(0), reader.GetString(1), Utc(reader.GetDateTime(2)),
                reader.IsDBNull(3) ? null : Utc(reader.GetDateTime(3)),
                reader.GetInt64(4), reader.GetInt64(5), reader.GetInt64(6)
            ));
        }
        return result;
    }

    public async Task<AutomaticLooseMediaPurgeResult> PurgeExpiredAsync(
        string dataConfigPath,
        string mediaStoreId,
        string cacheId,
        bool runtimeStoppedConfirmed,
        int batchSize = 1000,
        IProgress<AutomaticLooseMediaPruneProgress>? progress = null,
        CancellationToken token = default
    )
    {
        if (!runtimeStoppedConfirmed)
            throw new InvalidOperationException("Runtime stopped confirmation is required.");
        if (batchSize <= 0)
            throw new ArgumentOutOfRangeException(nameof(batchSize));
        ResolvedConfig config = VideoVariantLooseCleanupPreviewService.LoadConfig(dataConfigPath, mediaStoreId, cacheId);
        await using NpgsqlConnection connection = new(config.ConnectionString);
        await connection.OpenAsync(token);
        await EnsureSchemaAsync(connection, token);
        await AcquireLockAsync(connection, mediaStoreId, cacheId, token);
        try
        {
            long files = 0;
            long bytes = 0;
            while (true)
            {
                List<PruneOperation> operations = await LoadPurgeBatchAsync(
                    connection, mediaStoreId, cacheId, batchSize, token
                );
                if (operations.Count == 0)
                    break;
                foreach (PruneOperation operation in operations)
                {
                    token.ThrowIfCancellationRequested();
                    string quarantine = ResolveQuarantinePath(config, operation);
                    FileInfo info = new(quarantine);
                    if (info.Exists)
                    {
                        if (info.Length != operation.FileSize || info.LastWriteTimeUtc.Ticks != operation.LastWriteUtcTicks)
                            throw new InvalidOperationException($"Quarantine file changed: {quarantine}");
                        File.Delete(quarantine);
                    }
                    await MarkOperationAsync(connection, operation.RunId, operation.Index, "purged", token);
                    files++;
                    bytes += operation.FileSize;
                }
                await CompletePurgedRunsAsync(connection, token);
                progress?.Report(new("purging", files, 0, bytes));
            }
            await CompletePurgedRunsAsync(connection, token);
            return new(files, bytes);
        }
        finally
        {
            await ReleaseLockAsync(connection, mediaStoreId, cacheId, CancellationToken.None);
        }
    }

    private static async Task<AutomaticLooseMediaPruneRun> PlanAsync(
        NpgsqlConnection connection,
        ResolvedConfig config,
        string cacheNamespace,
        string policyHash,
        HashSet<string> desired,
        List<CacheRow> rows,
        CancellationToken token
    )
    {
        Dictionary<string, List<CacheRow>> normalizedRows = rows
            .GroupBy(row => Normalize(row.Path), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.ToList(), StringComparer.OrdinalIgnoreCase);
        HashSet<string> ambiguousGroups = rows
            .GroupBy(row => Group(row.Path), StringComparer.OrdinalIgnoreCase)
            .Where(group => group.Any(row => normalizedRows[Normalize(row.Path)].Count != 1))
            .Select(group => group.Key)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        Dictionary<string, string[]> desiredByGroup = desired
            .GroupBy(Group, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                group => group.Key,
                group => group.Order(StringComparer.OrdinalIgnoreCase).ToArray(),
                StringComparer.OrdinalIgnoreCase
            );
        Dictionary<string, CacheRow> keeps = [];
        foreach ((string group, string[] desiredPaths) in desiredByGroup)
        {
            if (ambiguousGroups.Contains(group))
                continue;
            List<CacheRow> desiredRows = [];
            bool complete = true;
            foreach (string desiredPath in desiredPaths)
            {
                if (
                    !normalizedRows.TryGetValue(desiredPath, out List<CacheRow>? matches)
                    || matches.Count != 1
                    || matches[0].PartitionId is null
                    || matches[0].FileSize is null
                )
                {
                    complete = false;
                    break;
                }
                CacheRow desiredRow = matches[0];
                string desiredFile = ResolveActivePath(
                    config,
                    desiredRow.PartitionId!.Value,
                    desiredRow.Path
                );
                FileInfo desiredInfo = new(desiredFile);
                if (!desiredInfo.Exists || desiredInfo.Length != desiredRow.FileSize!.Value)
                {
                    complete = false;
                    break;
                }
                RejectReparsePath(desiredFile, config.MediaRoots[desiredRow.PartitionId.Value]);
                desiredRows.Add(desiredRow);
            }
            if (complete)
                keeps[group] = desiredRows[0];
        }

        Guid runId = Guid.NewGuid();
        List<PruneOperation> operations = [];
        foreach (CacheRow row in rows)
        {
            string path = Normalize(row.Path);
            string group = Group(path);
            if (desired.Contains(path) || ambiguousGroups.Contains(group) || !keeps.TryGetValue(group, out CacheRow? keep))
                continue;
            if (row.PartitionId is null || row.FileSize is null || keep.PartitionId is null)
                continue;
            string source = ResolveActivePath(config, row.PartitionId.Value, path);
            string keepFile = ResolveActivePath(config, keep.PartitionId.Value, keep.Path);
            FileInfo sourceInfo = new(source);
            FileInfo keepInfo = new(keepFile);
            if (!sourceInfo.Exists || !keepInfo.Exists || sourceInfo.Length != row.FileSize.Value)
                continue;
            RejectReparsePath(source, config.MediaRoots[row.PartitionId.Value]);
            RejectReparsePath(keepFile, config.MediaRoots[keep.PartitionId.Value]);
            operations.Add(new(
                runId, operations.Count, path, row.PartitionId.Value, sourceInfo.Length,
                sourceInfo.LastWriteTimeUtc.Ticks, Normalize(keep.Path), keep.PartitionId.Value, "pending"
            ));
        }

        DateTimeOffset created = DateTimeOffset.UtcNow;
        await using NpgsqlTransaction transaction = await connection.BeginTransactionAsync(token);
        await using (NpgsqlCommand command = connection.CreateCommand())
        {
            command.Transaction = transaction;
            command.CommandText = """
                INSERT INTO media_loose_prune_runs
                    (run_id, media_store_id, cache_id, cache_namespace, policy_hash, state, created_utc, operation_count)
                VALUES (@id, @store, @cache, @namespace, @policy, 'planned', @created, @count)
                """;
            command.Parameters.AddWithValue("id", runId);
            command.Parameters.AddWithValue("store", config.MediaStoreId);
            command.Parameters.AddWithValue("cache", config.CacheId);
            command.Parameters.AddWithValue("namespace", cacheNamespace);
            command.Parameters.AddWithValue("policy", policyHash);
            command.Parameters.AddWithValue("created", created);
            command.Parameters.AddWithValue("count", operations.Count);
            await command.ExecuteNonQueryAsync(token);
        }
        if (operations.Count > 0)
        {
            await using NpgsqlBinaryImporter importer = await connection.BeginBinaryImportAsync(
                "COPY media_loose_prune_operations (run_id, operation_index, path, partition_id, file_size, last_write_utc_ticks, keep_path, keep_partition_id, state) FROM STDIN (FORMAT BINARY)", token
            );
            foreach (PruneOperation operation in operations)
            {
                await importer.StartRowAsync(token);
                await importer.WriteAsync(operation.RunId, NpgsqlDbType.Uuid, token);
                await importer.WriteAsync(operation.Index, NpgsqlDbType.Integer, token);
                await importer.WriteAsync(operation.Path, NpgsqlDbType.Text, token);
                await importer.WriteAsync(operation.PartitionId, NpgsqlDbType.Integer, token);
                await importer.WriteAsync(operation.FileSize, NpgsqlDbType.Bigint, token);
                await importer.WriteAsync(operation.LastWriteUtcTicks, NpgsqlDbType.Bigint, token);
                await importer.WriteAsync(operation.KeepPath, NpgsqlDbType.Text, token);
                await importer.WriteAsync(operation.KeepPartitionId, NpgsqlDbType.Integer, token);
                await importer.WriteAsync(operation.State, NpgsqlDbType.Text, token);
            }
            await importer.CompleteAsync(token);
        }
        await transaction.CommitAsync(token);
        return new(runId, config.MediaStoreId, config.CacheId, cacheNamespace, policyHash, "planned", created, operations.Count);
    }

    private static async Task<AutomaticLooseMediaPruneResult> ApplyAsync(
        NpgsqlConnection connection,
        ResolvedConfig config,
        AutomaticLooseMediaPruneRun run,
        int batchSize,
        IProgress<AutomaticLooseMediaPruneProgress>? progress,
        CancellationToken token
    )
    {
        if (run.State == "quarantined")
            return new(
                run.RunId,
                run.OperationCount,
                run.OperationCount,
                await LoadCompletedBytesAsync(connection, run.RunId, token),
                "quarantined",
                run.CreatedUtc.AddDays(RetentionDays)
            );
        await SetRunStateAsync(connection, run.RunId, "applying", token);
        long completed = 0;
        long bytes = 0;
        while (true)
        {
            List<PruneOperation> operations = await LoadApplyBatchAsync(connection, run.RunId, batchSize, token);
            if (operations.Count == 0)
                break;
            foreach (PruneOperation operation in operations)
            {
                token.ThrowIfCancellationRequested();
                string source = ResolveActivePath(config, operation.PartitionId, operation.Path);
                string keep = ResolveActivePath(config, operation.KeepPartitionId, operation.KeepPath);
                string quarantine = ResolveQuarantinePath(config, operation);
                FileInfo keepInfo = new(keep);
                if (!keepInfo.Exists)
                    throw new InvalidOperationException($"Keep file is absent: {keep}");
                FileInfo sourceInfo = new(source);
                FileInfo quarantineInfo = new(quarantine);
                if (sourceInfo.Exists)
                {
                    if (quarantineInfo.Exists)
                        throw new InvalidOperationException($"Both active and quarantine files exist: {source}");
                    if (sourceInfo.Length != operation.FileSize || sourceInfo.LastWriteTimeUtc.Ticks != operation.LastWriteUtcTicks)
                        throw new InvalidOperationException($"Active file changed after planning: {source}");
                    RejectReparsePath(source, config.MediaRoots[operation.PartitionId]);
                    Directory.CreateDirectory(Path.GetDirectoryName(quarantine)!);
                    File.Move(source, quarantine);
                }
                else if (!quarantineInfo.Exists)
                {
                    throw new InvalidOperationException($"Neither active nor quarantine file exists: {source}");
                }
                quarantineInfo.Refresh();
                if (quarantineInfo.Length != operation.FileSize || quarantineInfo.LastWriteTimeUtc.Ticks != operation.LastWriteUtcTicks)
                    throw new InvalidOperationException($"Quarantine fingerprint differs: {quarantine}");
                await DeleteCacheRowsAsync(
                    connection,
                    run.MediaStoreId,
                    run.CacheId,
                    run.CacheNamespace,
                    operation.Path,
                    token
                );
                await MarkOperationAsync(connection, run.RunId, operation.Index, "verified", token);
                completed++;
                bytes += operation.FileSize;
            }
            progress?.Report(new("applying", completed, run.OperationCount, bytes));
        }
        DateTimeOffset purgeAfter = await CompleteApplyAsync(connection, run.RunId, token);
        return new(
            run.RunId,
            run.OperationCount,
            run.OperationCount,
            await LoadCompletedBytesAsync(connection, run.RunId, token),
            "quarantined",
            purgeAfter
        );
    }

    private static async Task EnsureSchemaAsync(NpgsqlConnection connection, CancellationToken token)
    {
        await using NpgsqlCommand command = connection.CreateCommand();
        command.CommandText = """
            CREATE TABLE IF NOT EXISTS media_loose_prune_runs (
                run_id uuid PRIMARY KEY,
                media_store_id text NOT NULL,
                cache_id text NOT NULL,
                cache_namespace text NOT NULL,
                policy_hash text NOT NULL,
                state text NOT NULL,
                created_utc timestamptz NOT NULL,
                purge_after_utc timestamptz NULL,
                completed_utc timestamptz NULL,
                operation_count bigint NOT NULL
            );
            CREATE UNIQUE INDEX IF NOT EXISTS ux_media_loose_prune_active_run
                ON media_loose_prune_runs (media_store_id, cache_id)
                WHERE state IN ('planned', 'applying');
            CREATE TABLE IF NOT EXISTS media_loose_prune_operations (
                run_id uuid NOT NULL REFERENCES media_loose_prune_runs(run_id) ON DELETE RESTRICT,
                operation_index integer NOT NULL,
                path text NOT NULL,
                partition_id integer NOT NULL,
                file_size bigint NOT NULL,
                last_write_utc_ticks bigint NOT NULL,
                keep_path text NOT NULL,
                keep_partition_id integer NOT NULL,
                state text NOT NULL,
                updated_utc timestamptz NOT NULL DEFAULT CURRENT_TIMESTAMP,
                PRIMARY KEY (run_id, operation_index)
            );
            CREATE INDEX IF NOT EXISTS ix_media_loose_prune_operations_state
                ON media_loose_prune_operations (run_id, state, operation_index);
            """;
        await command.ExecuteNonQueryAsync(token);
    }

    private static async Task<string> DiscoverNamespaceAsync(NpgsqlConnection connection, string store, string cache, CancellationToken token)
    {
        await using NpgsqlCommand command = connection.CreateCommand();
        command.CommandText = "SELECT DISTINCT namespace FROM media_cache_primary_entries WHERE namespace LIKE @prefix ESCAPE '\\' ORDER BY namespace";
        command.Parameters.AddWithValue("prefix", $"backup:media-cache:{EscapeLike(store)}:{EscapeLike(cache)}|primary|%");
        List<string> values = [];
        await using NpgsqlDataReader reader = await command.ExecuteReaderAsync(token);
        while (await reader.ReadAsync(token)) values.Add(reader.GetString(0));
        if (values.Count != 1)
            throw new InvalidOperationException($"Expected exactly one primary cache namespace; found {values.Count}.");
        return values[0];
    }

    private static async Task<List<CacheRow>> LoadSupportedRowsAsync(NpgsqlConnection connection, string cacheNamespace, CancellationToken token)
    {
        await using NpgsqlCommand command = connection.CreateCommand();
        command.CommandTimeout = 600;
        command.CommandText = "SELECT replace(path, chr(92), '/'), file_size, partition_id FROM media_cache_primary_entries WHERE namespace = @namespace AND (path ILIKE '%/photo/%' OR path ILIKE '%/video/%' OR path ILIKE '%/gif/%') ORDER BY path";
        command.Parameters.AddWithValue("namespace", cacheNamespace);
        List<CacheRow> result = [];
        await using NpgsqlDataReader reader = await command.ExecuteReaderAsync(CommandBehavior.SequentialAccess, token);
        while (await reader.ReadAsync(token))
        {
            string path = reader.GetString(0);
            if (SupportedPath.IsMatch(path))
                result.Add(new(path, reader.IsDBNull(1) ? null : reader.GetInt64(1), reader.IsDBNull(2) ? null : reader.GetInt32(2)));
        }
        return result;
    }

    private static async Task<AutomaticLooseMediaPruneRun?> LoadActiveRunAsync(NpgsqlConnection connection, string store, string cache, CancellationToken token)
    {
        await using NpgsqlCommand command = connection.CreateCommand();
        command.CommandText = "SELECT run_id, media_store_id, cache_id, cache_namespace, policy_hash, state, created_utc, operation_count FROM media_loose_prune_runs WHERE media_store_id = @store AND cache_id = @cache AND state IN ('planned', 'applying') ORDER BY created_utc LIMIT 1";
        command.Parameters.AddWithValue("store", store);
        command.Parameters.AddWithValue("cache", cache);
        await using NpgsqlDataReader reader = await command.ExecuteReaderAsync(token);
        return await reader.ReadAsync(token)
            ? new(reader.GetGuid(0), reader.GetString(1), reader.GetString(2), reader.GetString(3), reader.GetString(4), reader.GetString(5), Utc(reader.GetDateTime(6)), reader.GetInt64(7))
            : null;
    }

    private static async Task<List<PruneOperation>> LoadApplyBatchAsync(NpgsqlConnection connection, Guid runId, int limit, CancellationToken token) =>
        await LoadOperationsAsync(connection, "o.run_id = @run AND o.state = 'pending'", runId, limit, token);

    private static async Task<List<PruneOperation>> LoadPurgeBatchAsync(NpgsqlConnection connection, string store, string cache, int limit, CancellationToken token) =>
        await LoadOperationsAsync(connection, "r.media_store_id = @store AND r.cache_id = @cache AND r.state IN ('quarantined', 'purging') AND r.purge_after_utc <= CURRENT_TIMESTAMP AND o.state = 'verified'", null, limit, token, store, cache);

    private static async Task<List<PruneOperation>> LoadOperationsAsync(NpgsqlConnection connection, string predicate, Guid? runId, int limit, CancellationToken token, string? store = null, string? cache = null)
    {
        await using NpgsqlCommand command = connection.CreateCommand();
        command.CommandText = $"SELECT o.run_id, o.operation_index, o.path, o.partition_id, o.file_size, o.last_write_utc_ticks, o.keep_path, o.keep_partition_id, o.state FROM media_loose_prune_operations o JOIN media_loose_prune_runs r ON r.run_id = o.run_id WHERE {predicate} ORDER BY r.created_utc, o.operation_index LIMIT @limit";
        if (runId.HasValue) command.Parameters.AddWithValue("run", runId.Value);
        if (store is not null) command.Parameters.AddWithValue("store", store);
        if (cache is not null) command.Parameters.AddWithValue("cache", cache);
        command.Parameters.AddWithValue("limit", limit);
        List<PruneOperation> result = [];
        await using NpgsqlDataReader reader = await command.ExecuteReaderAsync(token);
        while (await reader.ReadAsync(token))
            result.Add(new(reader.GetGuid(0), reader.GetInt32(1), reader.GetString(2), reader.GetInt32(3), reader.GetInt64(4), reader.GetInt64(5), reader.GetString(6), reader.GetInt32(7), reader.GetString(8)));
        return result;
    }

    private static async Task DeleteCacheRowsAsync(
        NpgsqlConnection connection,
        string mediaStoreId,
        string cacheId,
        string primaryNamespace,
        string path,
        CancellationToken token
    )
    {
        await using NpgsqlTransaction transaction = await connection.BeginTransactionAsync(token);
        await using NpgsqlCommand command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            DELETE FROM media_cache_primary_entries WHERE namespace = @primary AND lower(replace(path, chr(92), '/')) = lower(@path);
            DELETE FROM media_cache_incremental_entries WHERE namespace LIKE @incremental ESCAPE '\\' AND lower(replace(path, chr(92), '/')) = lower(@path);
            """;
        command.Parameters.AddWithValue("primary", primaryNamespace);
        command.Parameters.AddWithValue(
            "incremental",
            $"backup:media-cache:{EscapeLike(mediaStoreId)}:{EscapeLike(cacheId)}|incremental|%"
        );
        command.Parameters.AddWithValue("path", path);
        await command.ExecuteNonQueryAsync(token);
        await transaction.CommitAsync(token);
    }

    private static async Task MarkOperationAsync(NpgsqlConnection connection, Guid runId, int index, string state, CancellationToken token)
    {
        await using NpgsqlCommand command = connection.CreateCommand();
        command.CommandText = "UPDATE media_loose_prune_operations SET state = @state, updated_utc = CURRENT_TIMESTAMP WHERE run_id = @run AND operation_index = @index";
        command.Parameters.AddWithValue("state", state);
        command.Parameters.AddWithValue("run", runId);
        command.Parameters.AddWithValue("index", index);
        if (await command.ExecuteNonQueryAsync(token) != 1) throw new InvalidOperationException("Prune operation state update failed.");
    }

    private static async Task SetRunStateAsync(NpgsqlConnection connection, Guid runId, string state, CancellationToken token)
    {
        await using NpgsqlCommand command = connection.CreateCommand();
        command.CommandText = "UPDATE media_loose_prune_runs SET state = @state WHERE run_id = @run";
        command.Parameters.AddWithValue("state", state);
        command.Parameters.AddWithValue("run", runId);
        await command.ExecuteNonQueryAsync(token);
    }

    private static async Task<DateTimeOffset> CompleteApplyAsync(NpgsqlConnection connection, Guid runId, CancellationToken token)
    {
        await using NpgsqlCommand command = connection.CreateCommand();
        command.CommandText = "UPDATE media_loose_prune_runs SET state = 'quarantined', completed_utc = CURRENT_TIMESTAMP, purge_after_utc = CURRENT_TIMESTAMP + INTERVAL '7 days' WHERE run_id = @run AND NOT EXISTS (SELECT 1 FROM media_loose_prune_operations WHERE run_id = @run AND state <> 'verified') RETURNING purge_after_utc";
        command.Parameters.AddWithValue("run", runId);
        object? value = await command.ExecuteScalarAsync(token);
        return value is DateTimeOffset result ? result : throw new InvalidOperationException("Prune run is not fully verified.");
    }

    private static async Task<long> LoadCompletedBytesAsync(
        NpgsqlConnection connection,
        Guid runId,
        CancellationToken token
    )
    {
        await using NpgsqlCommand command = connection.CreateCommand();
        command.CommandText = "SELECT coalesce(sum(file_size), 0) FROM media_loose_prune_operations WHERE run_id = @run AND state IN ('verified', 'purged')";
        command.Parameters.AddWithValue("run", runId);
        return Convert.ToInt64(await command.ExecuteScalarAsync(token));
    }

    private static async Task CompletePurgedRunsAsync(NpgsqlConnection connection, CancellationToken token)
    {
        await using NpgsqlCommand command = connection.CreateCommand();
        command.CommandText = "UPDATE media_loose_prune_runs r SET state = 'purged' WHERE r.state IN ('quarantined', 'purging') AND r.purge_after_utc <= CURRENT_TIMESTAMP AND NOT EXISTS (SELECT 1 FROM media_loose_prune_operations o WHERE o.run_id = r.run_id AND o.state <> 'purged')";
        await command.ExecuteNonQueryAsync(token);
    }

    private static async Task AcquireLockAsync(NpgsqlConnection connection, string store, string cache, CancellationToken token)
    {
        await using NpgsqlCommand command = connection.CreateCommand();
        command.CommandText = "SELECT pg_advisory_lock(hashtextextended('media-cache:' || @namespace, 0))";
        command.Parameters.AddWithValue("namespace", $"backup:media-cache:{store}:{cache}");
        await command.ExecuteNonQueryAsync(token);
    }

    private static async Task ReleaseLockAsync(NpgsqlConnection connection, string store, string cache, CancellationToken token)
    {
        if (connection.State != ConnectionState.Open) return;
        await using NpgsqlCommand command = connection.CreateCommand();
        command.CommandText = "SELECT pg_advisory_unlock(hashtextextended('media-cache:' || @namespace, 0))";
        command.Parameters.AddWithValue("namespace", $"backup:media-cache:{store}:{cache}");
        await command.ExecuteNonQueryAsync(token);
    }

    private static string ResolveActivePath(ResolvedConfig config, int partitionId, string logicalPath)
    {
        if (!config.MediaRoots.TryGetValue(partitionId, out string? root))
            throw new InvalidOperationException($"Partition {partitionId} has no resolved media root.");
        return ResolveContained(root, logicalPath);
    }

    private static string ResolveQuarantinePath(ResolvedConfig config, PruneOperation operation)
    {
        string root = config.MediaRoots.TryGetValue(operation.PartitionId, out string? value)
            ? value : throw new InvalidOperationException($"Partition {operation.PartitionId} has no resolved media root.");
        return ResolveContained(root, $"{QuarantineDirectory}/{operation.RunId:N}/{operation.Path}");
    }

    private static string ResolveContained(string root, string logicalPath)
    {
        string fullRoot = Path.GetFullPath(root);
        string full = Path.GetFullPath(Path.Combine(fullRoot, logicalPath.Replace('/', Path.DirectorySeparatorChar)));
        string prefix = fullRoot.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
        StringComparison comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        if (!full.StartsWith(prefix, comparison)) throw new InvalidOperationException($"Path escapes media root: {logicalPath}");
        return full;
    }

    private static void RejectReparsePath(string path, string root)
    {
        string? current = Path.GetDirectoryName(path);
        string fullRoot = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        while (current is not null && current.Length >= fullRoot.Length)
        {
            if (Directory.Exists(current) && (File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0)
                throw new InvalidOperationException($"Reparse-point path is not eligible for prune: {path}");
            if (string.Equals(current.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar), fullRoot, OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal)) break;
            current = Path.GetDirectoryName(current);
        }
    }

    internal static string Group(string path)
    {
        string[] parts = Normalize(path).Split('/');
        if (parts.Length >= 4 && parts[1].Equals("photo", StringComparison.OrdinalIgnoreCase)) return string.Join('/', parts.Take(4));
        if (parts.Length >= 5 && parts[3].Equals("thumb", StringComparison.OrdinalIgnoreCase)) return string.Join('/', parts.Take(5));
        if (parts.Length >= 4 && parts[1].Equals("video", StringComparison.OrdinalIgnoreCase) && parts[3].Equals("mp4", StringComparison.OrdinalIgnoreCase)) return string.Join('/', parts.Take(3));
        throw new InvalidOperationException($"Unsupported media path: {path}");
    }

    internal static string Normalize(string path) => path.Replace('\\', '/').TrimStart('/');
    private static DateTimeOffset Utc(DateTime value) =>
        new(DateTime.SpecifyKind(value, DateTimeKind.Utc));
    private static string Hash(byte[] value) => Convert.ToHexString(SHA256.HashData(value)).ToLowerInvariant();
    private static string EscapeLike(string value) => value.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("%", "\\%", StringComparison.Ordinal).Replace("_", "\\_", StringComparison.Ordinal);

    private sealed record CacheRow(string Path, long? FileSize, int? PartitionId);
    private sealed record PruneOperation(Guid RunId, int Index, string Path, int PartitionId, long FileSize, long LastWriteUtcTicks, string KeepPath, int KeepPartitionId, string State);
    private sealed record AutomaticLooseMediaPruneRun(Guid RunId, string MediaStoreId, string CacheId, string CacheNamespace, string PolicyHash, string State, DateTimeOffset CreatedUtc, long OperationCount);
}

public sealed record AutomaticLooseMediaPruneResult(Guid RunId, long Operations, long Completed, long BytesQuarantined, string State, DateTimeOffset PurgeAfterUtc);
public sealed record AutomaticLooseMediaPurgeResult(long FilesDeleted, long BytesDeleted);
public sealed record AutomaticLooseMediaPruneProgress(string Phase, long Completed, long Total, long Bytes);
public sealed record AutomaticLooseMediaPruneStatus(Guid RunId, string State, DateTimeOffset CreatedUtc, DateTimeOffset? PurgeAfterUtc, long Operations, long Completed, long Bytes);
