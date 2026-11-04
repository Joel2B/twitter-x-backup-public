using System.Collections.Concurrent;
using System.Data;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Npgsql;

namespace Backup.Infrastructure.Media.Maintenance;

public sealed class VideoVariantLooseCleanupPreviewService
{
    internal const long DirectMinimumOriginalSize = 10 * 1024 * 1024;
    internal const string ParsedVideoCteSql = """
        normalized AS (
            SELECT replace(path, chr(92), '/') AS normalized_path,
                   stream_size, file_size, partition_id
            FROM media_cache_primary_entries
            WHERE namespace = @namespace
        ),
        parsed AS (
            SELECT n.normalized_path AS path,
                   n.stream_size, n.file_size, n.partition_id,
                   m[1] AS post_id, m[2] AS media_id,
                   m[3]::numeric AS width, m[4]::numeric AS height
            FROM normalized n
            CROSS JOIN LATERAL regexp_match(
                n.normalized_path,
                '^([^/]+)/video/([^/]+)/mp4/[^/]+/([1-9][0-9]*)x([1-9][0-9]*)\.mp4$'
            ) m
            WHERE position('/video/' in n.normalized_path) > 0
              AND position('/mp4/' in n.normalized_path) > 0
              AND right(n.normalized_path, 4) = '.mp4'
        )
        """;

    internal const string SelectionSql =
        "WITH "
        + ParsedVideoCteSql
        + """
            , collisions AS (
                SELECT lower(path) AS semantic_path
                FROM parsed
                GROUP BY lower(path)
                HAVING count(*) > 1
            ), ranked AS (
                SELECT p.*,
                       first_value(path) OVER candidate AS keep_path,
                       first_value(stream_size) OVER candidate AS keep_stream_size,
                       first_value(file_size) OVER candidate AS keep_file_size,
                       first_value(partition_id) OVER candidate AS keep_partition_id,
                       first_value(width) OVER candidate AS keep_width,
                       first_value(height) OVER candidate AS keep_height,
                       row_number() OVER candidate AS rn
                FROM parsed p
                WHERE NOT EXISTS (
                    SELECT 1 FROM collisions c WHERE c.semantic_path = lower(p.path)
                )
                WINDOW candidate AS (
                    PARTITION BY post_id, media_id
                    ORDER BY width * height DESC, width DESC, height DESC, path ASC
                )
            )
            SELECT path AS remove_path, stream_size AS remove_stream_size,
                   file_size AS remove_file_size, partition_id AS remove_partition_id,
                   post_id, media_id, width AS remove_width, height AS remove_height,
                   keep_path, keep_stream_size, keep_file_size, keep_partition_id,
                   keep_width, keep_height
            FROM ranked
            WHERE rn > 1
              AND width * height < keep_width * keep_height
              AND width <= keep_width
              AND height <= keep_height
            ORDER BY remove_path
            """;

    internal const string CollisionSql =
        "WITH "
        + ParsedVideoCteSql
        + """
            SELECT lower(path) AS semantic_path, min(path) AS representative_path,
                   count(*) AS path_count, count(DISTINCT path) AS exact_path_count
            FROM parsed
            GROUP BY lower(path)
            HAVING count(*) > 1
            ORDER BY semantic_path
            """;

    internal const string PreflightRowsSql =
        "WITH "
        + ParsedVideoCteSql
        + """
            SELECT path, stream_size, file_size, partition_id
            FROM parsed
            ORDER BY path
            """;

    internal const string ParsedGifThumbCteSql = """
        normalized AS (
            SELECT replace(path, chr(92), '/') AS normalized_path,
                   stream_size, file_size, partition_id
            FROM media_cache_primary_entries
            WHERE namespace = @namespace
        ),
        parsed AS (
            SELECT n.normalized_path AS path,
                   n.stream_size, n.file_size, n.partition_id
            FROM normalized n
            WHERE n.normalized_path ~* '^[^/]+/gif/[^/]+/thumb/[^/]+/(jpg|png)/(dimension|size)/[^/]+\.(jpg|png)$'
        )
        """;

    internal const string GifThumbRowsSql =
        "WITH "
        + ParsedGifThumbCteSql
        + " SELECT path, stream_size, file_size, partition_id FROM parsed ORDER BY path";

    internal const string ParsedVideoThumbCteSql = """
        normalized AS (
            SELECT replace(path, chr(92), '/') AS normalized_path,
                   stream_size, file_size, partition_id
            FROM media_cache_primary_entries
            WHERE namespace = @namespace
        ),
        parsed AS (
            SELECT n.normalized_path AS path,
                   n.stream_size, n.file_size, n.partition_id
            FROM normalized n
            WHERE n.normalized_path ~* '^[^/]+/video/[^/]+/thumb/[^/]+/'
        )
        """;

    internal const string VideoThumbRowsSql =
        "WITH "
        + ParsedVideoThumbCteSql
        + " SELECT path, stream_size, file_size, partition_id FROM parsed ORDER BY path";

    internal const string ParsedPhotoCteSql = """
        normalized AS (
            SELECT replace(path, chr(92), '/') AS normalized_path,
                   stream_size, file_size, partition_id
            FROM media_cache_primary_entries
            WHERE namespace = @namespace
        ),
        parsed AS (
            SELECT n.normalized_path AS path,
                   n.stream_size, n.file_size, n.partition_id
            FROM normalized n
            WHERE n.normalized_path ~* '^[^/]+/photo/[^/]+/[^/]+/'
        )
        """;

    internal const string PhotoRowsSql =
        "WITH "
        + ParsedPhotoCteSql
        + " SELECT path, stream_size, file_size, partition_id FROM parsed ORDER BY path";

    private static readonly Regex VideoPathPattern = new(
        @"^(?<post>[^/]+)/video/(?<media>[^/]+)/mp4/(?<video>[^/]+)/(?<width>[1-9]\d*)x(?<height>[1-9]\d*)\.mp4$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase
    );
    private static readonly Regex GifThumbPathPattern = new(
        @"^(?<post>[^/]+)/gif/(?<media>[^/]+)/thumb/(?<thumb>[^/]+)/(?<format>jpg|png)/(?<resolutionType>dimension|size)/(?<name>[^/.]+)\.(?<extension>jpg|png)$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase
    );
    private static readonly Regex VideoThumbPathPattern = new(
        @"^(?<post>[^/]+)/video/(?<media>[^/]+)/thumb/(?<thumb>[^/]+)/(?<format>jpg|png)/(?<resolutionType>dimension|size)/(?<name>[^/.]+)\.(?<extension>jpg|png)$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase
    );
    private static readonly Regex PhotoPathPattern = new(
        @"^(?<post>[^/]+)/photo/(?<media>[^/]+)/(?<source>[^/]+)/(?<format>jpg|png)/(?<resolutionType>dimension|size)/(?<name>[^/.]+)\.(?<extension>jpg|png)$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase
    );

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true,
    };

    public async Task<VideoVariantLooseCleanupManifest> PreviewAsync(
        string dataConfigPath,
        string mediaStoreId,
        string cacheId,
        string directRoot,
        int fileParallelism = 16,
        int? fileLimit = null,
        int dbCommandTimeoutSeconds = 600,
        bool dbOnly = false,
        string selectionKind = "video-variants",
        IProgress<VideoVariantLooseCleanupProgress>? progress = null,
        CancellationToken token = default
    )
    {
        Stopwatch stopwatch = Stopwatch.StartNew();
        ValidateTimeout(dbCommandTimeoutSeconds);
        ValidateFileOptions(fileParallelism, fileLimit);
        ValidateSelectionKind(selectionKind);
        ResolvedConfig config = LoadConfig(dataConfigPath, mediaStoreId, cacheId);
        string fullDirectRoot = RequireAbsolutePath(directRoot, nameof(directRoot));

        await using NpgsqlConnection connection = new(config.ConnectionString);
        await connection.OpenAsync(token);
        await using NpgsqlTransaction transaction = await connection.BeginTransactionAsync(
            IsolationLevel.RepeatableRead,
            token
        );
        await SetReadOnly(connection, transaction, dbCommandTimeoutSeconds, token);
        string cacheNamespace = await DiscoverNamespace(
            connection,
            transaction,
            mediaStoreId,
            cacheId,
            dbCommandTimeoutSeconds,
            token
        );
        long scanned;
        long videos;
        bool groupedImageSelection = selectionKind is "gif-thumbs" or "video-thumbs" or "photo-variants";
        string parsedCte = selectionKind switch
        {
            "gif-thumbs" => ParsedGifThumbCteSql,
            "video-thumbs" => ParsedVideoThumbCteSql,
            "photo-variants" => ParsedPhotoCteSql,
            _ => ParsedVideoCteSql,
        };

        await using (NpgsqlCommand command = connection.CreateCommand())
        {
            command.Transaction = transaction;
            command.CommandTimeout = dbCommandTimeoutSeconds;
            command.CommandText =
                "WITH "
                + parsedCte
                + """
                    SELECT
                        (SELECT count(*) FROM media_cache_primary_entries WHERE namespace = @namespace),
                        (SELECT count(*) FROM parsed)
                    """;
            command.Parameters.AddWithValue("namespace", cacheNamespace);
            await using NpgsqlDataReader reader = await command.ExecuteReaderAsync(
                CommandBehavior.SequentialAccess,
                token
            );
            if (!await reader.ReadAsync(token))
                throw new InvalidOperationException("Database metrics query returned no row.");
            scanned = reader.GetInt64(0);
            videos = reader.GetInt64(1);
        }
        progress?.Report(new("db-count", scanned, videos, 0, 0, 0, stopwatch.Elapsed));

        List<VideoVariantLooseCleanupIssue> selectionIssues = [];

        if (selectionKind == "video-variants")
        {
            await using NpgsqlCommand command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandTimeout = dbCommandTimeoutSeconds;
            command.CommandText = CollisionSql;
            command.Parameters.AddWithValue("namespace", cacheNamespace);
            await using NpgsqlDataReader reader = await command.ExecuteReaderAsync(
                CommandBehavior.SequentialAccess,
                token
            );

            while (await reader.ReadAsync(token))
            {
                selectionIssues.Add(
                    new(
                        reader.GetString(1),
                        reader.GetInt64(3) == 1 ? "duplicate-path" : "case-collision",
                        $"{reader.GetInt64(2)} cache paths share normalized lowercase key '{reader.GetString(0)}'."
                    )
                );
            }
        }

        List<VideoVariantLooseSelection> selections = [];
        List<VideoVariantLooseCacheRow>? thumbnailRows = null;
        long selectionsRead = 0;

        if (groupedImageSelection)
        {
            thumbnailRows = [];
            await using NpgsqlCommand command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandTimeout = dbCommandTimeoutSeconds;
            command.CommandText = selectionKind switch
            {
                "gif-thumbs" => GifThumbRowsSql,
                "video-thumbs" => VideoThumbRowsSql,
                _ => PhotoRowsSql,
            };
            command.Parameters.AddWithValue("namespace", cacheNamespace);
            await using NpgsqlDataReader reader = await command.ExecuteReaderAsync(
                CommandBehavior.SequentialAccess,
                token
            );

            while (await reader.ReadAsync(token))
            {
                thumbnailRows.Add(
                    new(
                        reader.GetString(0),
                        NullableInt64(reader, 1),
                        NullableInt64(reader, 2),
                        NullableInt32(reader, 3)
                    )
                );
            }
            selections = selectionKind switch
            {
                "gif-thumbs" => SelectGifThumbCandidates(thumbnailRows, selectionIssues),
                "video-thumbs" => SelectVideoThumbCandidates(thumbnailRows, selectionIssues),
                _ => SelectPhotoCandidates(thumbnailRows, selectionIssues),
            };
            selectionsRead = selections.Count;
            if (fileLimit.HasValue)
                selections = selections.Take(fileLimit.Value).ToList();
        }
        else
        {
            await using NpgsqlCommand command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandTimeout = dbCommandTimeoutSeconds;
            command.CommandText = SelectionSql;
            command.Parameters.AddWithValue("namespace", cacheNamespace);
            await using NpgsqlDataReader reader = await command.ExecuteReaderAsync(
                CommandBehavior.SequentialAccess,
                token
            );

            while (await reader.ReadAsync(token))
            {
                selectionsRead++;
                if (!fileLimit.HasValue || selections.Count < fileLimit.Value)
                {
                    selections.Add(
                        new(
                            new(
                                reader.GetString(0),
                                NullableInt64(reader, 1),
                                NullableInt64(reader, 2),
                                NullableInt32(reader, 3)
                            ),
                            new(
                                reader.GetString(8),
                                NullableInt64(reader, 9),
                                NullableInt64(reader, 10),
                                NullableInt32(reader, 11)
                            )
                        )
                    );
                }
                if (selectionsRead % 50_000 == 0)
                    progress?.Report(
                        new(
                            "db-select",
                            scanned,
                            videos,
                            checked((int)Math.Min(selectionsRead, int.MaxValue)),
                            0,
                            selectionIssues.Count,
                            stopwatch.Elapsed
                        )
                    );
            }
        }
        int totalSelections = checked((int)Math.Min(selectionsRead, int.MaxValue));
        progress?.Report(
            new(
                "db-select",
                scanned,
                videos,
                totalSelections,
                totalSelections,
                selectionIssues.Count,
                stopwatch.Elapsed
            )
        );

        await transaction.CommitAsync(token);
        if (selectionKind == "photo-variants")
            thumbnailRows = null;
        VideoVariantLooseCleanupManifest manifest = BuildManifest(
            config,
            cacheNamespace,
            fullDirectRoot,
            selections,
            selectionIssues,
            scanned,
            videos,
            fileParallelism,
            fileLimit,
            dbOnly: dbOnly || groupedImageSelection,
            progress: progress,
            elapsed: () => stopwatch.Elapsed,
            token: token
        );
        if (groupedImageSelection && selectionKind != "photo-variants" && !dbOnly)
        {
            Dictionary<string, VideoVariantLooseCacheRow> exactRows = thumbnailRows!
                .GroupBy(row => row.Path.Replace('\\', '/'), StringComparer.Ordinal)
                .Where(group => group.Count() == 1)
                .ToDictionary(group => group.Key, group => group.Single(), StringComparer.Ordinal);
            VideoVariantLooseCleanupPreflightResult physical = await BuildPhysicalManifestAsync(
                manifest,
                config.MediaRoots,
                exactRows,
                progress,
                token
            );
            if (!physical.IsValid || physical.PhysicalManifest is null)
                throw new InvalidOperationException(
                    $"Grouped image physical preview failed: {string.Join("; ", physical.Errors.Take(10))}"
                );
            manifest = physical.PhysicalManifest;
        }
        progress?.Report(
            new(
                "done",
                scanned,
                videos,
                manifest.Operations.Count + manifest.Issues.Count,
                manifest.Operations.Count + manifest.Issues.Count,
                manifest.Issues.Count,
                stopwatch.Elapsed
            )
        );
        return manifest;
    }

    public void SaveManifest(VideoVariantLooseCleanupManifest manifest, string outputPath)
    {
        ValidateManifest(manifest, verifyHash: true);
        string fullPath = Path.GetFullPath(outputPath);
        string? directory = Path.GetDirectoryName(fullPath);
        if (string.IsNullOrEmpty(directory) || !Directory.Exists(directory))
            throw new DirectoryNotFoundException(
                $"Manifest output directory does not exist: {directory}"
            );

        string tempPath = $"{fullPath}.{Guid.NewGuid():N}.tmp";
        try
        {
            using (FileStream stream = File.Create(tempPath))
                JsonSerializer.Serialize(stream, manifest, JsonOptions);
            File.Move(tempPath, fullPath, overwrite: true);
        }
        finally
        {
            if (File.Exists(tempPath))
                File.Delete(tempPath);
        }
    }

    public VideoVariantLooseCleanupManifest LoadManifest(string manifestPath)
    {
        using FileStream stream = File.OpenRead(manifestPath);
        VideoVariantLooseCleanupManifest manifest =
            JsonSerializer.Deserialize<VideoVariantLooseCleanupManifest>(stream, JsonOptions)
            ?? throw new InvalidOperationException("Invalid loose cleanup manifest.");
        ValidateManifest(manifest, verifyHash: true);
        return manifest;
    }

    public async Task<VideoVariantLooseCleanupPreflightResult> PreflightAsync(
        VideoVariantLooseCleanupManifest manifest,
        int dbCommandTimeoutSeconds = 600,
        IProgress<VideoVariantLooseCleanupProgress>? progress = null,
        CancellationToken token = default
    )
    {
        ValidateManifest(manifest, verifyHash: true);
        ValidateTimeout(dbCommandTimeoutSeconds);
        List<string> errors = [];
        FileInfo configInfo = new(manifest.DataConfigPath);
        if (
            !configInfo.Exists
            || configInfo.Length != manifest.DataConfigLength
            || configInfo.LastWriteTimeUtc.Ticks != manifest.DataConfigLastWriteUtcTicks
        )
        {
            errors.Add("Data config fingerprint changed.");
            return Result(manifest, errors);
        }

        ResolvedConfig config;
        try
        {
            config = LoadConfig(manifest.DataConfigPath, manifest.MediaStoreId, manifest.CacheId);
        }
        catch (Exception ex) when (ex is IOException or JsonException or InvalidOperationException)
        {
            errors.Add($"Data config is invalid: {ex.Message}");
            return Result(manifest, errors);
        }

        await using NpgsqlConnection connection = new(config.ConnectionString);
        await connection.OpenAsync(token);
        await using NpgsqlTransaction transaction = await connection.BeginTransactionAsync(
            IsolationLevel.RepeatableRead,
            token
        );
        await SetReadOnly(connection, transaction, dbCommandTimeoutSeconds, token);
        string currentNamespace = await DiscoverNamespace(
            connection,
            transaction,
            manifest.MediaStoreId,
            manifest.CacheId,
            dbCommandTimeoutSeconds,
            token
        );
        if (!currentNamespace.Equals(manifest.CacheNamespace, StringComparison.Ordinal))
            errors.Add("Cache namespace changed.");

        HashSet<string> targetPaths = manifest
            .Operations.SelectMany(operation =>
                new[]
                {
                    manifest.Paths[operation.RemovePathIndex],
                    manifest.Paths[operation.KeepPathIndex],
                }
            )
            .ToHashSet(StringComparer.Ordinal);
        Dictionary<string, List<VideoVariantLooseCacheRow>> dbRows = new(StringComparer.Ordinal);
        long rowsScanned = 0;

        await using (NpgsqlCommand command = connection.CreateCommand())
        {
            command.Transaction = transaction;
            command.CommandTimeout = dbCommandTimeoutSeconds;
            command.CommandText = GetPreflightRowsSql(manifest);
            command.Parameters.AddWithValue("namespace", manifest.CacheNamespace);
            await using NpgsqlDataReader reader = await command.ExecuteReaderAsync(
                CommandBehavior.SequentialAccess,
                token
            );

            while (await reader.ReadAsync(token))
            {
                rowsScanned++;
                AddTargetDbRow(
                    targetPaths,
                    dbRows,
                    new(
                        reader.GetString(0),
                        reader.IsDBNull(1) ? null : reader.GetInt64(1),
                        reader.IsDBNull(2) ? null : reader.GetInt64(2),
                        reader.IsDBNull(3) ? null : reader.GetInt32(3)
                    )
                );
                if (rowsScanned % 50_000 == 0)
                    progress?.Report(
                        new(
                            "preflight-db",
                            manifest.CacheRowsScanned,
                            manifest.VideoEntries,
                            checked((int)Math.Min(rowsScanned, int.MaxValue)),
                            checked((int)Math.Min(manifest.VideoEntries, int.MaxValue)),
                            errors.Count
                        )
                    );
            }
        }
        progress?.Report(
            new(
                "preflight-db",
                manifest.CacheRowsScanned,
                manifest.VideoEntries,
                checked((int)Math.Min(rowsScanned, int.MaxValue)),
                checked((int)Math.Min(manifest.VideoEntries, int.MaxValue)),
                errors.Count
            )
        );

        await transaction.CommitAsync(token);
        Dictionary<string, VideoVariantLooseCacheRow> exactRows = new(StringComparer.Ordinal);
        HashSet<string> validatedPaths = new(StringComparer.Ordinal);

        foreach (VideoVariantLooseCleanupOperation operation in manifest.Operations)
        {
            string remove = manifest.Paths[operation.RemovePathIndex];
            string keep = manifest.Paths[operation.KeepPathIndex];
            if (validatedPaths.Add(remove))
                ValidateDbRow(
                    remove,
                    operation.PartitionId,
                    operation.OriginalSize,
                    dbRows,
                    errors
                );
            if (validatedPaths.Add(keep))
                ValidateDbRow(keep, null, null, dbRows, errors);
        }

        foreach ((string path, List<VideoVariantLooseCacheRow> rows) in dbRows)
            if (rows.Count == 1)
                exactRows[path] = rows[0];

        VideoVariantLooseCleanupPreflightResult physical = await BuildPhysicalManifestAsync(
            manifest,
            config.MediaRoots,
            exactRows,
            progress,
            token
        );
        errors.AddRange(physical.Errors);
        return physical with
        {
            IsValid = errors.Count == 0,
            Errors = errors,
            PhysicalManifest = physical.PhysicalManifest,
        };
    }

    internal static async Task<VideoVariantLooseCleanupPreflightResult> BuildPhysicalManifestAsync(
        VideoVariantLooseCleanupManifest manifest,
        IReadOnlyDictionary<int, string> mediaRoots,
        IReadOnlyDictionary<string, VideoVariantLooseCacheRow> dbRows,
        IProgress<VideoVariantLooseCleanupProgress>? progress = null,
        CancellationToken token = default
    )
    {
        List<string> errors = [];
        List<VideoVariantLooseCleanupIssue> setupIssues = [];
        HashSet<(string Path, string Code)> fatalKeys = [];
        HashSet<string> invalidSetupGroups = new(StringComparer.OrdinalIgnoreCase);
        Dictionary<string, RootWork> rootWork = new(PathComparer);
        Dictionary<string, KeepCheck> keeps = new(StringComparer.Ordinal);
        List<RemoveCheck> removes = [];

        foreach (
            (VideoVariantLooseCleanupOperation operation, int index) in manifest.Operations.Select(
                (value, index) => (value, index)
            )
        )
        {
            string remove = manifest.Paths[operation.RemovePathIndex];
            string keep = manifest.Paths[operation.KeepPathIndex];
            if (!mediaRoots.TryGetValue(operation.PartitionId, out string? removeRoot))
            {
                AddFatal(
                    remove,
                    "partition-unresolved",
                    $"Candidate partition {operation.PartitionId} is unresolved."
                );
                continue;
            }
            if (
                !dbRows.TryGetValue(keep, out VideoVariantLooseCacheRow? keepRow)
                || keepRow.PartitionId is not int keepPartitionId
                || !mediaRoots.TryGetValue(keepPartitionId, out string? keepRoot)
            )
            {
                AddFatal(
                    keep,
                    "keep-partition-unresolved",
                    "Keep path partition is null or unresolved."
                );
                continue;
            }

            RootWork removeWork = GetRootWork(removeRoot, operation.PartitionId);
            RemoveCheck removeCheck = new(index, remove, keep, operation, removeWork.Key);
            removes.Add(removeCheck);
            removeWork.Removes.Add(removeCheck);
            if (!keeps.ContainsKey(keep))
            {
                RootWork keepWork = GetRootWork(keepRoot, keepPartitionId);
                KeepCheck keepCheck = new(keep, keepWork.Key);
                keeps.Add(keep, keepCheck);
                keepWork.Keeps.Add(keepCheck);
            }
        }

        Task<RootResult>[] rootTasks = rootWork
            .Values.OrderBy(work => work.Key, PathComparer)
            .Select(work => Task.Run(() => CheckRoot(work, manifest, progress, token), token))
            .ToArray();
        Task<DirectResult> directTask = Task.Run(
            () => CheckDirect(manifest, removes, progress, token),
            token
        );
        RootResult[] rootResults = await Task.WhenAll(rootTasks);
        Dictionary<int, FileCheck> removeResults = rootResults
            .SelectMany(result => result.Removes)
            .ToDictionary(result => result.Index);
        Dictionary<string, bool> keepResults = rootResults
            .SelectMany(result => result.Keeps)
            .ToDictionary(result => result.Path, result => result.Exists, StringComparer.Ordinal);

        DirectResult direct = await directTask;
        List<PendingOperation> pending = [];
        List<VideoVariantLooseCleanupIssue> physicalIssues = rootResults
            .SelectMany(result => result.Issues)
            .ToList();

        foreach (
            KeepCheck keep in keeps.Values.OrderBy(value => value.Path, StringComparer.Ordinal)
        )
            if (!keepResults.GetValueOrDefault(keep.Path))
                AddFatal(keep.Path, "keep-missing", "Keep path is absent from original storage.");

        foreach (RemoveCheck remove in removes.OrderBy(value => value.Index))
        {
            if (
                !removeResults.TryGetValue(remove.Index, out FileCheck? original)
                || !original.Exists
            )
            {
                physicalIssues.Add(
                    new(
                        remove.Path,
                        "cache-stale",
                        "Cache candidate is absent from original storage."
                    )
                );
                continue;
            }
            if (original.Size != remove.Operation.OriginalSize)
            {
                AddFatal(
                    remove.Path,
                    "size-mismatch",
                    "Original length differs from DB/cache size."
                );
                continue;
            }
            if (
                manifest.PhysicalChecked
                && original.LastWriteUtcTicks != remove.Operation.OriginalLastWriteUtcTicks
            )
            {
                AddFatal(remove.Path, "fingerprint-changed", "Original fingerprint changed.");
                continue;
            }
            if (!keepResults.GetValueOrDefault(remove.Keep))
                continue;

            FileCheck? directFile = direct.Files.GetValueOrDefault(remove.Index);
            pending.Add(
                new(
                    remove.Path,
                    remove.Keep,
                    remove.Operation.PartitionId,
                    dbRows[remove.Keep].PartitionId!.Value,
                    original.Size,
                    original.LastWriteUtcTicks,
                    directFile?.Exists == true,
                    directFile?.Exists == true ? directFile.Size : 0,
                    directFile?.Exists == true ? directFile.LastWriteUtcTicks : 0
                )
            );
        }

        physicalIssues.AddRange(setupIssues);
        List<VideoVariantLooseCleanupIssue> ungroupedIssues = physicalIssues
            .Where(issue => !IsPhotoPath(issue.Path))
            .ToList();
        ungroupedIssues.AddRange(
            physicalIssues
                .Where(issue => IsPhotoPath(issue.Path))
                .GroupBy(
                    issue => GetGroupedImageDirectory(issue.Path),
                    StringComparer.OrdinalIgnoreCase
                )
                .OrderBy(group => group.Key, StringComparer.Ordinal)
                .Select(group => new VideoVariantLooseCleanupIssue(
                    group.Key,
                    "keep-set-invalid",
                    "Image group failed physical validation and was preserved."
                ))
        );
        physicalIssues = ungroupedIssues;
        HashSet<string> invalidImageGroups = physicalIssues
            .Where(issue => IsGroupedImagePath(issue.Path))
            .Select(issue => GetGroupedImageDirectory(issue.Path))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (invalidImageGroups.Count > 0)
            pending.RemoveAll(operation =>
                IsGroupedImagePath(operation.Remove)
                && invalidImageGroups.Contains(GetGroupedImageDirectory(operation.Remove))
            );
        VideoVariantLooseCleanupManifest physicalManifest = CreatePhysicalManifest(
            manifest,
            pending,
            physicalIssues
        );
        int originalFiles = rootResults.Sum(result => result.OriginalFiles);
        long originalBytes = rootResults.Sum(result => result.OriginalBytes);
        return new()
        {
            IsValid = errors.Count == 0,
            Errors = errors,
            Operations = physicalManifest.Operations.Count,
            OriginalFiles = originalFiles,
            OriginalBytes = originalBytes,
            DirectFiles = direct.Files.Values.Count(file => file.Exists),
            DirectBytes = direct.Files.Values.Where(file => file.Exists).Sum(file => file.Size),
            PhysicalManifest = physicalManifest,
        };

        RootWork GetRootWork(string root, int partitionId)
        {
            string key = Path.GetFullPath(root);
            if (!rootWork.TryGetValue(key, out RootWork? work))
            {
                string label =
                    Path.GetPathRoot(key)
                        ?.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                    ?? $"partition-{partitionId}";
                rootWork.Add(key, work = new(key, label));
            }
            return work;
        }

        void AddFatal(string path, string code, string message)
        {
            if (!fatalKeys.Add((path, code)))
                return;
            if (IsPhotoPath(path))
            {
                string group = GetGroupedImageDirectory(path);
                if (invalidSetupGroups.Add(group))
                    setupIssues.Add(
                        new(
                            group,
                            "keep-set-invalid",
                            "Image group failed physical validation and was preserved."
                        )
                    );
                return;
            }
            setupIssues.Add(new(path, code, message));
            errors.Add($"{path}: {message}");
        }
    }

    private static RootResult CheckRoot(
        RootWork work,
        VideoVariantLooseCleanupManifest manifest,
        IProgress<VideoVariantLooseCleanupProgress>? progress,
        CancellationToken token
    )
    {
        if (
            work.Keeps.All(item => IsGroupedImagePath(item.Path))
            && work.Removes.All(item => IsGroupedImagePath(item.Path))
        )
            return CheckGroupedImageRoot(work, manifest, progress, token);

        List<KeepFileCheck> keeps = [];
        List<FileCheck> removes = [];
        int checkedItems = 0;
        int total = work.Keeps.Count + work.Removes.Count;

        foreach (KeepCheck keep in work.Keeps.OrderBy(value => value.Path, StringComparer.Ordinal))
        {
            token.ThrowIfCancellationRequested();
            FileInfo info = new(ResolveContainedPath(work.Key, keep.Path));
            keeps.Add(new(keep.Path, info.Exists));
            Report();
        }

        foreach (RemoveCheck remove in work.Removes.OrderBy(value => value.Index))
        {
            token.ThrowIfCancellationRequested();
            FileInfo info = new(ResolveContainedPath(work.Key, remove.Path));
            removes.Add(
                new(
                    remove.Index,
                    info.Exists,
                    info.Exists ? info.Length : 0,
                    info.Exists ? info.LastWriteTimeUtc.Ticks : 0
                )
            );
            Report();
        }
        return new(work.Key, keeps, removes, []);

        void Report()
        {
            checkedItems++;
            if (checkedItems % 1_000 == 0 || checkedItems == total)
                progress?.Report(
                    new(
                        "preflight-root",
                        manifest.CacheRowsScanned,
                        manifest.VideoEntries,
                        checkedItems,
                        total,
                        0,
                        RootLabel: work.Label,
                        RootChecked: checkedItems,
                        RootTotal: total
                    )
                );
        }
    }

    private static RootResult CheckGroupedImageRoot(
        RootWork work,
        VideoVariantLooseCleanupManifest manifest,
        IProgress<VideoVariantLooseCleanupProgress>? progress,
        CancellationToken token
    )
    {
        Dictionary<string, List<KeepCheck>> keepsByDirectory = work
            .Keeps.GroupBy(
                item => GetGroupedImageDirectory(item.Path),
                StringComparer.OrdinalIgnoreCase
            )
            .ToDictionary(
                group => group.Key,
                group => group.ToList(),
                StringComparer.OrdinalIgnoreCase
            );
        Dictionary<string, List<RemoveCheck>> removesByDirectory = work
            .Removes.GroupBy(
                item => GetGroupedImageDirectory(item.Path),
                StringComparer.OrdinalIgnoreCase
            )
            .ToDictionary(
                group => group.Key,
                group => group.ToList(),
                StringComparer.OrdinalIgnoreCase
            );
        string[] directories = keepsByDirectory
            .Keys.Concat(removesByDirectory.Keys)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        KeepFileCheck[] keepResults = new KeepFileCheck[work.Keeps.Count];
        FileCheck[] removeResults = new FileCheck[work.Removes.Count];
        Dictionary<string, int> keepIndices = work
            .Keeps.Select((item, index) => (item.Path, index))
            .ToDictionary(item => item.Path, item => item.index, StringComparer.Ordinal);
        Dictionary<int, int> removeIndices = work
            .Removes.Select((item, index) => (item.Index, index))
            .ToDictionary(item => item.Index, item => item.index);
        int checkedItems = 0;
        int total = work.Keeps.Count + work.Removes.Count;
        List<VideoVariantLooseCleanupIssue> issues = [];
        object issueLock = new();

        Parallel.ForEach(
            directories,
            new ParallelOptions
            {
                MaxDegreeOfParallelism = manifest.FileParallelism,
                CancellationToken = token,
            },
            directory =>
            {
                string fullDirectory = ResolveContainedPath(work.Key, directory);
                Dictionary<string, FileInfo> files = Directory.Exists(fullDirectory)
                    ? Directory
                        .EnumerateFiles(fullDirectory, "*", SearchOption.AllDirectories)
                        .Select(path => new FileInfo(path))
                        .ToDictionary(
                            info =>
                                Path.GetRelativePath(work.Key, info.FullName).Replace('\\', '/'),
                            StringComparer.OrdinalIgnoreCase
                        )
                    : new(StringComparer.OrdinalIgnoreCase);
                HashSet<string> expected = keepsByDirectory
                    .GetValueOrDefault(directory, [])
                    .Select(item => item.Path)
                    .Concat(
                        removesByDirectory
                            .GetValueOrDefault(directory, [])
                            .Select(item => item.Path)
                    )
                    .ToHashSet(StringComparer.OrdinalIgnoreCase);

                foreach (string unexpected in files.Keys.Where(path => !expected.Contains(path)))
                    lock (issueLock)
                        issues.Add(
                            new(
                                unexpected,
                                "unexpected-image-file",
                                "Image variant directory contains an unexpected physical file."
                            )
                        );

                foreach (KeepCheck keep in keepsByDirectory.GetValueOrDefault(directory, []))
                {
                    keepResults[keepIndices[keep.Path]] = new(
                        keep.Path,
                        files.ContainsKey(keep.Path)
                    );
                    Report();
                }

                foreach (RemoveCheck remove in removesByDirectory.GetValueOrDefault(directory, []))
                {
                    FileInfo? info = files.GetValueOrDefault(remove.Path);
                    removeResults[removeIndices[remove.Index]] = new(
                        remove.Index,
                        info is not null,
                        info?.Length ?? 0,
                        info?.LastWriteTimeUtc.Ticks ?? 0
                    );
                    Report();
                }
            }
        );

        return new(work.Key, [.. keepResults], [.. removeResults], issues);

        void Report()
        {
            int completed = Interlocked.Increment(ref checkedItems);
            if (completed % 1_000 == 0 || completed == total)
                progress?.Report(
                    new(
                        "preflight-root",
                        manifest.CacheRowsScanned,
                        manifest.VideoEntries,
                        completed,
                        total,
                        0,
                        RootLabel: work.Label,
                        RootChecked: completed,
                        RootTotal: total
                    )
                );
        }
    }

    private static bool IsGroupedImagePath(string path)
    {
        string[] parts = path.Replace('\\', '/').Split('/');
        return parts.Length >= 4
            && (
                parts[1].Equals("photo", StringComparison.OrdinalIgnoreCase)
                || (
                    parts.Length >= 5
                    && (
                        parts[1].Equals("gif", StringComparison.OrdinalIgnoreCase)
                        || parts[1].Equals("video", StringComparison.OrdinalIgnoreCase)
                    )
                    && parts[3].Equals("thumb", StringComparison.OrdinalIgnoreCase)
                )
            );
    }

    private static bool IsPhotoPath(string path)
    {
        string[] parts = path.Replace('\\', '/').Split('/');
        return parts.Length >= 4 && parts[1].Equals("photo", StringComparison.OrdinalIgnoreCase);
    }

    private static string GetGroupedImageDirectory(string path)
    {
        string[] parts = path.Replace('\\', '/').Split('/');
        if (parts.Length >= 4 && parts[1].Equals("photo", StringComparison.OrdinalIgnoreCase))
            return string.Join('/', parts.Take(4));
        if (
            parts.Length < 5
            || !(
                parts[1].Equals("gif", StringComparison.OrdinalIgnoreCase)
                || parts[1].Equals("video", StringComparison.OrdinalIgnoreCase)
            )
            || !parts[3].Equals("thumb", StringComparison.OrdinalIgnoreCase)
        )
            throw new InvalidOperationException($"Invalid grouped image path: {path}");
        return string.Join('/', parts.Take(5));
    }

    private static DirectResult CheckDirect(
        VideoVariantLooseCleanupManifest manifest,
        IReadOnlyCollection<RemoveCheck> removes,
        IProgress<VideoVariantLooseCleanupProgress>? progress,
        CancellationToken token
    )
    {
        RemoveCheck[] eligible = removes
            .Where(remove => remove.Operation.OriginalSize > DirectMinimumOriginalSize)
            .OrderBy(remove => remove.Index)
            .ToArray();
        ConcurrentDictionary<int, FileCheck> files = [];
        int checkedItems = 0;

        Parallel.ForEach(
            eligible,
            new ParallelOptions
            {
                MaxDegreeOfParallelism = manifest.FileParallelism,
                CancellationToken = token,
            },
            remove =>
            {
                FileInfo info = new(ResolveContainedPath(manifest.DirectRoot, remove.Path));
                files[remove.Index] = new(
                    remove.Index,
                    info.Exists,
                    info.Exists ? info.Length : 0,
                    info.Exists ? info.LastWriteTimeUtc.Ticks : 0
                );
                int completed = Interlocked.Increment(ref checkedItems);
                if (completed % 1_000 == 0 || completed == eligible.Length)
                    progress?.Report(
                        new(
                            "preflight-root",
                            manifest.CacheRowsScanned,
                            manifest.VideoEntries,
                            completed,
                            eligible.Length,
                            0,
                            RootLabel: "direct",
                            RootChecked: completed,
                            RootTotal: eligible.Length
                        )
                    );
            }
        );
        return new(files.ToDictionary());
    }

    private static VideoVariantLooseCleanupManifest CreatePhysicalManifest(
        VideoVariantLooseCleanupManifest source,
        IReadOnlyCollection<PendingOperation> pending,
        IReadOnlyCollection<VideoVariantLooseCleanupIssue> issues
    )
    {
        string[] paths = pending
            .SelectMany(operation => new[] { operation.Remove, operation.Keep })
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToArray();
        Dictionary<string, int> indices = paths
            .Select((path, index) => (path, index))
            .ToDictionary(value => value.path, value => value.index, StringComparer.Ordinal);
        VideoVariantLooseCleanupManifest physical = source with
        {
            FileParallelism = 1,
            PhysicalChecked = true,
            Paths = [.. paths],
            Operations = pending
                .OrderBy(operation => operation.Remove, StringComparer.Ordinal)
                .Select(operation => new VideoVariantLooseCleanupOperation
                {
                    RemovePathIndex = indices[operation.Remove],
                    KeepPathIndex = indices[operation.Keep],
                    PartitionId = operation.PartitionId,
                    KeepPartitionId = operation.KeepPartitionId,
                    OriginalSize = operation.OriginalSize,
                    OriginalLastWriteUtcTicks = operation.OriginalLastWriteUtcTicks,
                    DirectPresent = operation.DirectPresent,
                    DirectSize = operation.DirectSize,
                    DirectLastWriteUtcTicks = operation.DirectLastWriteUtcTicks,
                })
                .ToList(),
            Issues = source
                .Issues.Concat(issues)
                .OrderBy(issue => issue.Path, StringComparer.Ordinal)
                .ThenBy(issue => issue.Code, StringComparer.Ordinal)
                .ToList(),
            ApprovalHash = string.Empty,
        };
        physical.ApprovalHash = CalculateApprovalHash(physical);
        return physical;
    }

    internal static List<VideoVariantLooseSelection> SelectCandidates(
        IEnumerable<VideoVariantLooseCacheRow> rows,
        List<VideoVariantLooseCleanupIssue> issues
    )
    {
        List<ParsedRow> parsed = [];

        foreach (VideoVariantLooseCacheRow row in rows)
        {
            string path = row.Path.Replace('\\', '/');
            if (TryParseVideo(path, out string group, out int width, out int height))
                parsed.Add(new(row with { Path = path }, group, width, height));
        }

        foreach (
            IGrouping<string, ParsedRow> collision in parsed.GroupBy(
                item => item.Row.Path,
                StringComparer.OrdinalIgnoreCase
            )
        )
        {
            if (
                collision.Select(item => item.Row.Path).Distinct(StringComparer.Ordinal).Count() > 1
            )
                issues.Add(
                    new(
                        collision.First().Row.Path,
                        "case-collision",
                        "Cache paths differ only by case."
                    )
                );
        }

        foreach (
            IGrouping<string, ParsedRow> duplicate in parsed.GroupBy(
                item => item.Row.Path,
                StringComparer.Ordinal
            )
        )
        {
            if (duplicate.Count() > 1)
                issues.Add(
                    new(duplicate.Key, "duplicate-path", "Cache contains duplicate semantic paths.")
                );
        }

        HashSet<string> invalid = issues
            .Where(issue => issue.Code is "case-collision" or "duplicate-path")
            .Select(issue => issue.Path)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        List<VideoVariantLooseSelection> result = [];

        foreach (
            IGrouping<string, ParsedRow> group in parsed
                .Where(item => !invalid.Contains(item.Row.Path))
                .GroupBy(item => item.Group, StringComparer.OrdinalIgnoreCase)
        )
        {
            List<ParsedRow> ordered = group
                .OrderByDescending(item => (long)item.Width * item.Height)
                .ThenByDescending(item => item.Width)
                .ThenByDescending(item => item.Height)
                .ThenBy(item => item.Row.Path, StringComparer.Ordinal)
                .ToList();
            if (ordered.Count < 2)
                continue;
            ParsedRow keep = ordered[0];
            long keepArea = (long)keep.Width * keep.Height;

            foreach (ParsedRow remove in ordered.Skip(1))
            {
                if (
                    (long)remove.Width * remove.Height < keepArea
                    && remove.Width <= keep.Width
                    && remove.Height <= keep.Height
                )
                    result.Add(new(remove.Row, keep.Row));
            }
        }

        return result
            .OrderBy(item => item.Remove.Path, StringComparer.Ordinal)
            .ThenBy(item => item.Keep.Path, StringComparer.Ordinal)
            .ToList();
    }

    internal static List<VideoVariantLooseSelection> SelectGifThumbCandidates(
        IEnumerable<VideoVariantLooseCacheRow> rows,
        List<VideoVariantLooseCleanupIssue> issues
    ) => SelectThumbCandidates(rows, issues, GifThumbPathPattern, "GIF");

    internal static List<VideoVariantLooseSelection> SelectVideoThumbCandidates(
        IEnumerable<VideoVariantLooseCacheRow> rows,
        List<VideoVariantLooseCleanupIssue> issues
    ) => SelectThumbCandidates(rows, issues, VideoThumbPathPattern, "Video");

    internal static List<VideoVariantLooseSelection> SelectPhotoCandidates(
        IEnumerable<VideoVariantLooseCacheRow> rows,
        List<VideoVariantLooseCleanupIssue> issues
    ) => SelectThumbCandidates(rows, issues, PhotoPathPattern, "Photo");

    private static List<VideoVariantLooseSelection> SelectThumbCandidates(
        IEnumerable<VideoVariantLooseCacheRow> rows,
        List<VideoVariantLooseCleanupIssue> issues,
        Regex pathPattern,
        string mediaKind
    )
    {
        List<ParsedThumbRow> parsed = [];
        HashSet<string> malformedGroups = new(StringComparer.OrdinalIgnoreCase);

        foreach (VideoVariantLooseCacheRow row in rows)
        {
            string path = row.Path.Replace('\\', '/');
            Match match = pathPattern.Match(path);
            if (!match.Success)
            {
                if (TryGetGroupedImageGroup(path, out string? malformedGroup))
                {
                    malformedGroups.Add(malformedGroup!);
                    issues.Add(
                        new(
                            path,
                            mediaKind == "Photo"
                                ? "unexpected-image-path"
                                : "unexpected-thumbnail-path",
                            $"{mediaKind} image path has an unsupported shape."
                        )
                    );
                }
                continue;
            }
            if (
                !match
                    .Groups["format"]
                    .Value.Equals(
                        match.Groups["extension"].Value,
                        StringComparison.OrdinalIgnoreCase
                    )
            )
            {
                if (TryGetGroupedImageGroup(path, out string? mismatchGroup))
                    malformedGroups.Add(mismatchGroup!);
                issues.Add(
                    new(path, "format-extension-mismatch", "Image format and extension differ.")
                );
                continue;
            }

            parsed.Add(
                new(
                    row with
                    {
                        Path = path,
                    },
                    mediaKind == "Photo"
                        ? $"{match.Groups["post"].Value}/photo/{match.Groups["media"].Value}/{match.Groups["source"].Value}"
                        : $"{match.Groups["post"].Value}/{mediaKind.ToLowerInvariant()}/{match.Groups["media"].Value}/thumb/{match.Groups["thumb"].Value}",
                    match.Groups["format"].Value.ToLowerInvariant(),
                    match.Groups["resolutionType"].Value.ToLowerInvariant(),
                    match.Groups["name"].Value.ToLowerInvariant()
                )
            );
        }

        HashSet<string> invalidPaths = AddPathCollisionIssues(
            parsed.Select(item => item.Row),
            issues
        );
        foreach (ParsedThumbRow item in parsed.Where(item => invalidPaths.Contains(item.Row.Path)))
            malformedGroups.Add(item.Group);
        List<VideoVariantLooseSelection> result = [];

        foreach (
            IGrouping<string, ParsedThumbRow> group in parsed
                .Where(item =>
                    !invalidPaths.Contains(item.Row.Path) && !malformedGroups.Contains(item.Group)
                )
                .GroupBy(item => item.Group, StringComparer.OrdinalIgnoreCase)
        )
        {
            List<ParsedThumbRow> pngOrig = group
                .Where(item =>
                    item.Format == "png" && item.ResolutionType == "size" && item.Name == "orig"
                )
                .ToList();
            List<ParsedThumbRow> jpgOrig = group
                .Where(item =>
                    item.Format == "jpg" && item.ResolutionType == "size" && item.Name == "orig"
                )
                .ToList();
            List<ParsedThumbRow> jpgLarge = group
                .Where(item =>
                    item.Format == "jpg" && item.ResolutionType == "size" && item.Name == "large"
                )
                .ToList();
            bool pngSource = group.Any(item => item.Format == "png");
            List<ParsedThumbRow> keeps = pngSource
                ? pngOrig.Count == 1 && jpgLarge.Count == 1
                    ? [pngOrig[0], jpgLarge[0]]
                    : []
                : jpgOrig.Count == 1
                    ? [jpgOrig[0]]
                    : [];

            if (keeps.Count == 0)
            {
                issues.Add(
                    new(
                        group
                            .OrderBy(item => item.Row.Path, StringComparer.Ordinal)
                            .First()
                            .Row.Path,
                        "keep-set-invalid",
                        $"{mediaKind} image group lacks one unambiguous keep set."
                    )
                );
                continue;
            }

            HashSet<string> keepPaths = keeps
                .Select(item => item.Row.Path)
                .ToHashSet(StringComparer.Ordinal);
            int keepIndex = 0;
            result.AddRange(
                group
                    .Where(item => !keepPaths.Contains(item.Row.Path))
                    .Select(item => new VideoVariantLooseSelection(
                        item.Row,
                        keeps[keepIndex++ % keeps.Count].Row
                    ))
            );
        }

        return result.OrderBy(item => item.Remove.Path, StringComparer.Ordinal).ToList();
    }

    private static bool TryGetGroupedImageGroup(string path, out string? group)
    {
        string[] parts = path.Replace('\\', '/').Split('/');
        if (parts.Length >= 4 && parts[1].Equals("photo", StringComparison.OrdinalIgnoreCase))
        {
            group = string.Join('/', parts.Take(4));
            return true;
        }
        if (parts.Length >= 5 && parts[1] is "gif" or "video" && parts[3] == "thumb")
        {
            group = string.Join('/', parts.Take(5));
            return true;
        }
        group = null;
        return false;
    }

    private static HashSet<string> AddPathCollisionIssues(
        IEnumerable<VideoVariantLooseCacheRow> rows,
        List<VideoVariantLooseCleanupIssue> issues
    )
    {
        HashSet<string> invalid = new(StringComparer.OrdinalIgnoreCase);

        foreach (
            IGrouping<string, VideoVariantLooseCacheRow> group in rows.GroupBy(
                item => item.Path,
                StringComparer.OrdinalIgnoreCase
            )
        )
        {
            if (group.Count() <= 1)
                continue;
            int exact = group.Select(item => item.Path).Distinct(StringComparer.Ordinal).Count();
            issues.Add(
                new(
                    group.First().Path,
                    exact == 1 ? "duplicate-path" : "case-collision",
                    "Cache contains duplicate semantic paths."
                )
            );

            foreach (VideoVariantLooseCacheRow row in group)
                invalid.Add(row.Path);
        }

        return invalid;
    }

    internal static string? ResolvePartitionRoot(
        IReadOnlyList<string> segments,
        IReadOnlyDictionary<string, string> aliases
    )
    {
        if (segments.Count == 0)
            return null;
        string first = segments[0];
        if (first.StartsWith('@'))
        {
            if (first.Length == 1 || !aliases.TryGetValue(first[1..], out first!))
                return null;
        }

        if (Regex.IsMatch(first, @"^[A-Za-z]:$", RegexOptions.CultureInvariant))
            first += Path.DirectorySeparatorChar;

        if (!IsFullyQualifiedPath(first))
            return null;
        return Path.GetFullPath(Path.Combine([first, .. segments.Skip(1)]));
    }

    internal static string ResolveContainedPath(string root, string logicalPath)
    {
        string fullRoot = Path.GetFullPath(root);
        string relativePath = logicalPath
            .Replace('/', Path.DirectorySeparatorChar)
            .Replace('\\', Path.DirectorySeparatorChar);
        string fullPath = Path.GetFullPath(Path.Combine(fullRoot, relativePath));
        string relative = Path.GetRelativePath(fullRoot, fullPath);
        if (
            relative == ".."
            || relative.StartsWith($"..{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
            || Path.IsPathRooted(relative)
        )
            throw new InvalidOperationException($"Path escapes root: {logicalPath}");
        return fullPath;
    }

    internal static VideoVariantLooseCleanupManifest BuildManifest(
        ResolvedConfig config,
        string cacheNamespace,
        string directRoot,
        IReadOnlyCollection<VideoVariantLooseCacheRow> rows,
        long scanned,
        long videos,
        int fileParallelism = 16,
        int? fileLimit = null,
        DateTimeOffset? createdUtc = null,
        bool dbOnly = false,
        IProgress<VideoVariantLooseCleanupProgress>? progress = null,
        CancellationToken token = default
    )
    {
        List<VideoVariantLooseCleanupIssue> issues = [.. config.Issues];
        List<VideoVariantLooseSelection> selections = SelectCandidates(rows, issues);
        return BuildManifest(
            config,
            cacheNamespace,
            directRoot,
            selections,
            issues,
            scanned,
            videos,
            fileParallelism,
            fileLimit,
            createdUtc,
            dbOnly,
            progress,
            null,
            token
        );
    }

    internal static VideoVariantLooseCleanupManifest BuildManifest(
        ResolvedConfig config,
        string cacheNamespace,
        string directRoot,
        IReadOnlyCollection<VideoVariantLooseSelection> selected,
        IReadOnlyCollection<VideoVariantLooseCleanupIssue> selectionIssues,
        long scanned,
        long videos,
        int fileParallelism = 16,
        int? fileLimit = null,
        DateTimeOffset? createdUtc = null,
        bool dbOnly = false,
        IProgress<VideoVariantLooseCleanupProgress>? progress = null,
        Func<TimeSpan>? elapsed = null,
        CancellationToken token = default
    )
    {
        ValidateFileOptions(fileParallelism, fileLimit);
        List<VideoVariantLooseCleanupIssue> issues = [.. config.Issues, .. selectionIssues];
        List<VideoVariantLooseSelection> selections = selected
            .OrderBy(item => item.Remove.Path, StringComparer.Ordinal)
            .ThenBy(item => item.Keep.Path, StringComparer.Ordinal)
            .ToList();
        if (fileLimit.HasValue)
            selections = selections.Take(fileLimit.Value).ToList();
        if (dbOnly)
            return BuildDatabaseManifest(
                config,
                cacheNamespace,
                directRoot,
                selections,
                issues,
                scanned,
                videos,
                fileParallelism,
                fileLimit,
                createdUtc,
                token
            );
        SelectionCheckResult?[] results = new SelectionCheckResult[selections.Count];
        int checkedSelections = 0;
        int issueCount = issues.Count;

        {
            progress?.Report(
                new(
                    "file-check",
                    scanned,
                    videos,
                    0,
                    selections.Count,
                    issues.Count,
                    elapsed?.Invoke() ?? default
                )
            );

            Parallel.ForEach(
                Enumerable.Range(0, selections.Count),
                new ParallelOptions
                {
                    MaxDegreeOfParallelism = fileParallelism,
                    CancellationToken = token,
                },
                index =>
                {
                    VideoVariantLooseSelection selection = selections[index];
                    List<VideoVariantLooseCleanupIssue> localIssues = [];
                    PendingOperation? operation = null;
                    string remove = selection.Remove.Path;
                    if (
                        selection.Remove.PartitionId is not int partitionId
                        || !config.MediaRoots.TryGetValue(partitionId, out string? mediaRoot)
                    )
                    {
                        localIssues.Add(
                            new(
                                remove,
                                "partition-unresolved",
                                "Candidate partition is null, unavailable, or unresolved."
                            )
                        );
                    }
                    else if (
                        selection.Keep.PartitionId is not int keepPartitionId
                        || !config.MediaRoots.TryGetValue(
                            keepPartitionId,
                            out string? keepMediaRoot
                        )
                    )
                    {
                        localIssues.Add(
                            new(
                                remove,
                                "keep-partition-unresolved",
                                "Keep path partition is null or unresolved."
                            )
                        );
                    }
                    else
                    {
                        string original = ResolveContainedPath(mediaRoot, remove);
                        string keep = ResolveContainedPath(keepMediaRoot, selection.Keep.Path);
                        if (!File.Exists(keep))
                            localIssues.Add(
                                new(
                                    remove,
                                    "keep-missing",
                                    "Keep path is absent from original storage."
                                )
                            );
                        else
                        {
                            FileInfo originalInfo = new(original);
                            if (!originalInfo.Exists)
                                localIssues.Add(
                                    new(
                                        remove,
                                        "cache-stale",
                                        "Cache candidate is absent from original storage."
                                    )
                                );
                            else
                            {
                                string direct = ResolveContainedPath(directRoot, remove);
                                FileInfo directInfo = new(direct);
                                operation = new(
                                    remove,
                                    selection.Keep.Path,
                                    partitionId,
                                    keepPartitionId,
                                    originalInfo.Length,
                                    originalInfo.LastWriteTimeUtc.Ticks,
                                    directInfo.Exists,
                                    directInfo.Exists ? directInfo.Length : 0,
                                    directInfo.Exists ? directInfo.LastWriteTimeUtc.Ticks : 0
                                );
                            }
                        }
                    }

                    results[index] = new(operation, localIssues);
                    if (localIssues.Count > 0)
                        Interlocked.Add(ref issueCount, localIssues.Count);
                    int completed = Interlocked.Increment(ref checkedSelections);
                    if (completed % 1_000 == 0)
                        progress?.Report(
                            new(
                                "file-check",
                                scanned,
                                videos,
                                completed,
                                selections.Count,
                                Volatile.Read(ref issueCount),
                                elapsed?.Invoke() ?? default
                            )
                        );
                }
            );
        }

        List<PendingOperation> pending = results
            .Where(result => result!.Operation is not null)
            .Select(result => result!.Operation!)
            .ToList();
        issues.AddRange(results.SelectMany(result => result!.Issues));

        progress?.Report(
            new(
                "file-check",
                scanned,
                videos,
                checkedSelections,
                selections.Count,
                issues.Count,
                elapsed?.Invoke() ?? default
            )
        );

        string[] paths = pending
            .SelectMany(item => new[] { item.Remove, item.Keep })
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToArray();
        Dictionary<string, int> indices = paths
            .Select((path, index) => (path, index))
            .ToDictionary(x => x.path, x => x.index, StringComparer.Ordinal);
        VideoVariantLooseCleanupManifest manifest = new()
        {
            Version = 1,
            CreatedUtc = createdUtc ?? DateTimeOffset.UtcNow,
            DataConfigPath = config.DataConfigPath,
            DataConfigLength = config.DataConfigLength,
            DataConfigLastWriteUtcTicks = config.DataConfigLastWriteUtcTicks,
            MediaStoreId = config.MediaStoreId,
            CacheId = config.CacheId,
            CacheNamespace = cacheNamespace,
            DirectRoot = directRoot,
            CacheRowsScanned = scanned,
            VideoEntries = videos,
            FileParallelism = fileParallelism,
            SelectionLimit = fileLimit,
            PhysicalChecked = !dbOnly,
            Paths = [.. paths],
            Operations = pending
                .OrderBy(item => item.Remove, StringComparer.Ordinal)
                .Select(item => new VideoVariantLooseCleanupOperation
                {
                    RemovePathIndex = indices[item.Remove],
                    KeepPathIndex = indices[item.Keep],
                    PartitionId = item.PartitionId,
                    OriginalSize = item.OriginalSize,
                    OriginalLastWriteUtcTicks = item.OriginalLastWriteUtcTicks,
                    DirectPresent = item.DirectPresent,
                    DirectSize = item.DirectSize,
                    DirectLastWriteUtcTicks = item.DirectLastWriteUtcTicks,
                })
                .ToList(),
            Issues = issues
                .OrderBy(issue => issue.Path, StringComparer.Ordinal)
                .ThenBy(issue => issue.Code, StringComparer.Ordinal)
                .ToList(),
        };
        manifest.ApprovalHash = CalculateApprovalHash(manifest);
        return manifest;
    }

    private static VideoVariantLooseCleanupManifest BuildDatabaseManifest(
        ResolvedConfig config,
        string cacheNamespace,
        string directRoot,
        List<VideoVariantLooseSelection> selections,
        List<VideoVariantLooseCleanupIssue> issues,
        long scanned,
        long videos,
        int fileParallelism,
        int? fileLimit,
        DateTimeOffset? createdUtc,
        CancellationToken token
    )
    {
        HashSet<string> invalidGroups = selections
            .Where(selection =>
                IsGroupedImagePath(selection.Remove.Path)
                && (
                    selection.Remove.PartitionId is not int partitionId
                    || !config.MediaRoots.ContainsKey(partitionId)
                    || selection.Keep.PartitionId is not int keepPartitionId
                    || !config.MediaRoots.ContainsKey(keepPartitionId)
                    || selection.Remove.FileSize is null
                )
            )
            .Select(selection => GetGroupedImageDirectory(selection.Remove.Path))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (string group in invalidGroups.Order(StringComparer.Ordinal))
            issues.Add(
                new(
                    group,
                    "keep-set-invalid",
                    "Image group contains unresolved partition or file size metadata."
                )
            );
        if (invalidGroups.Count > 0)
            selections.RemoveAll(selection =>
                invalidGroups.Contains(GetGroupedImageDirectory(selection.Remove.Path))
            );

        for (int index = selections.Count - 1; index >= 0; index--)
        {
            token.ThrowIfCancellationRequested();
            VideoVariantLooseSelection selection = selections[index];
            string remove = selection.Remove.Path;
            string? code = null;
            string? message = null;
            if (
                selection.Remove.PartitionId is not int partitionId
                || !config.MediaRoots.ContainsKey(partitionId)
            )
            {
                code = "partition-unresolved";
                message = "Candidate partition is null, unavailable, or unresolved.";
            }
            else if (
                selection.Keep.PartitionId is not int keepPartitionId
                || !config.MediaRoots.ContainsKey(keepPartitionId)
            )
            {
                code = "keep-partition-unresolved";
                message = "Keep path partition is null or unresolved.";
            }
            else if (selection.Remove.FileSize is null)
            {
                code = "cache-size-null";
                message = "Candidate cache file_size is null.";
            }
            if (code is null)
                continue;
            issues.Add(new(remove, code, message!));
            selections.RemoveAt(index);
        }

        string[] paths = selections
            .SelectMany(item => new[] { item.Remove.Path, item.Keep.Path })
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToArray();
        Dictionary<string, int> indices = paths
            .Select((path, index) => (path, index))
            .ToDictionary(value => value.path, value => value.index, StringComparer.Ordinal);
        List<VideoVariantLooseCleanupOperation> operations = selections
            .Select(item => new VideoVariantLooseCleanupOperation
            {
                RemovePathIndex = indices[item.Remove.Path],
                KeepPathIndex = indices[item.Keep.Path],
                PartitionId = item.Remove.PartitionId!.Value,
                OriginalSize = item.Remove.FileSize!.Value,
            })
            .ToList();
        selections.Clear();

        VideoVariantLooseCleanupManifest manifest = new()
        {
            Version = 1,
            CreatedUtc = createdUtc ?? DateTimeOffset.UtcNow,
            DataConfigPath = config.DataConfigPath,
            DataConfigLength = config.DataConfigLength,
            DataConfigLastWriteUtcTicks = config.DataConfigLastWriteUtcTicks,
            MediaStoreId = config.MediaStoreId,
            CacheId = config.CacheId,
            CacheNamespace = cacheNamespace,
            DirectRoot = directRoot,
            CacheRowsScanned = scanned,
            VideoEntries = videos,
            FileParallelism = fileParallelism,
            SelectionLimit = fileLimit,
            PhysicalChecked = false,
            Paths = [.. paths],
            Operations = operations,
            Issues = issues
                .OrderBy(issue => issue.Path, StringComparer.Ordinal)
                .ThenBy(issue => issue.Code, StringComparer.Ordinal)
                .ToList(),
        };
        manifest.ApprovalHash = CalculateApprovalHash(manifest);
        return manifest;
    }

    internal static string CalculateApprovalHash(VideoVariantLooseCleanupManifest manifest)
    {
        VideoVariantLooseCleanupManifest canonical = manifest with { ApprovalHash = string.Empty };
        using SHA256 sha256 = SHA256.Create();
        using CryptoStream stream = new(Stream.Null, sha256, CryptoStreamMode.Write);
        JsonSerializer.Serialize(stream, canonical, JsonOptions);
        stream.FlushFinalBlock();
        return Convert.ToHexString(sha256.Hash!).ToLowerInvariant();
    }

    internal static ResolvedConfig LoadConfig(string path, string mediaStoreId, string cacheId)
    {
        string fullPath = Path.GetFullPath(path);
        FileInfo info = new(fullPath);
        if (!info.Exists)
            throw new FileNotFoundException("Data config not found.", fullPath);

        using JsonDocument document = JsonDocument.Parse(File.ReadAllBytes(fullPath));
        JsonElement root = RequireObject(document.RootElement, "Data config");
        Dictionary<string, string> aliases = ReadStringDictionary(
            RequireProperty(root, "Aliases", "Data config"),
            "Data config.Aliases"
        );
        Dictionary<int, ParsedPartition> partitions = [];

        foreach (
            JsonElement element in RequireArray(
                RequireProperty(root, "Partitions", "Data config"),
                "Data config.Partitions"
            )
        )
        {
            JsonElement partition = RequireObject(element, "Data config.Partitions item");
            int id = RequireInt32(
                RequireProperty(partition, "Id", "Data config.Partitions item"),
                "Data config.Partitions item.Id"
            );
            ParsedPartition parsed = new(
                ReadOptionalBoolean(partition, "Enabled", true, $"Partition {id}"),
                ReadStringArray(
                    RequireProperty(partition, "Paths", $"Partition {id}"),
                    $"Partition {id}.Paths"
                )
            );
            if (!partitions.TryAdd(id, parsed))
                throw new InvalidOperationException($"Partition Id must be unique: {id}");
        }

        JsonElement store = SelectById(
            RequireProperty(root, "Media", "Data config"),
            mediaStoreId,
            "Data config.Media",
            "media store"
        );
        if (!ReadOptionalBoolean(store, "Enabled", true, $"Media store '{mediaStoreId}'"))
            throw new InvalidOperationException($"Media store is disabled: {mediaStoreId}");

        JsonElement cache = SelectById(
            RequireProperty(store, "Cache", $"Media store '{mediaStoreId}'"),
            cacheId,
            $"Media store '{mediaStoreId}'.Cache",
            "media cache"
        );
        bool cacheEnabled = ReadOptionalBoolean(cache, "Enabled", true, $"Media cache '{cacheId}'");
        string cacheType = RequireString(
            RequireProperty(cache, "Type", $"Media cache '{cacheId}'"),
            $"Media cache '{cacheId}'.Type"
        );
        string connectionString = ResolveEnvironmentValue(
            RequireString(
                RequireProperty(cache, "ConnectionString", $"Media cache '{cacheId}'"),
                $"Media cache '{cacheId}'.ConnectionString"
            )
        );
        if (
            !cacheEnabled
            || !cacheType.Equals("postgres", StringComparison.OrdinalIgnoreCase)
            || string.IsNullOrWhiteSpace(connectionString)
        )
            throw new InvalidOperationException(
                "Media cache must be enabled postgres with ConnectionString."
            );

        JsonElement storePaths = RequireObject(
            RequireProperty(store, "Paths", $"Media store '{mediaStoreId}'"),
            $"Media store '{mediaStoreId}'.Paths"
        );
        JsonElement mediaPaths = RequireObject(
            RequireProperty(storePaths, "Media", $"Media store '{mediaStoreId}'.Paths"),
            $"Media store '{mediaStoreId}'.Paths.Media"
        );
        List<string> mediaPathSegments = ReadStringArray(
            RequireProperty(mediaPaths, "Paths", $"Media store '{mediaStoreId}'.Paths.Media"),
            $"Media store '{mediaStoreId}'.Paths.Media.Paths"
        );
        List<int> storePartitionIds = ReadInt32Array(
            RequireProperty(store, "Partitions", $"Media store '{mediaStoreId}'"),
            $"Media store '{mediaStoreId}'.Partitions"
        );

        Dictionary<int, string> roots = [];
        List<VideoVariantLooseCleanupIssue> issues = [];

        foreach (int partitionId in storePartitionIds.Distinct())
        {
            string? partitionRoot =
                partitions.TryGetValue(partitionId, out ParsedPartition? partition)
                && partition.Enabled
                    ? ResolvePartitionRoot(partition.Paths, aliases)
                    : null;
            if (partitionRoot is null)
            {
                issues.Add(
                    new(
                        string.Empty,
                        "partition-unresolved",
                        $"Partition {partitionId} is disabled, missing, relative, or has an unresolved alias."
                    )
                );
                continue;
            }
            roots[partitionId] = Path.GetFullPath(
                Path.Combine([partitionRoot, .. mediaPathSegments])
            );
        }

        return new(
            fullPath,
            info.Length,
            info.LastWriteTimeUtc.Ticks,
            mediaStoreId,
            cacheId,
            connectionString,
            roots,
            issues
        );
    }

    private static string ResolveEnvironmentValue(string value)
    {
        const string prefix = "env:";
        if (!value.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            return value;
        string name = value[prefix.Length..];
        if (string.IsNullOrWhiteSpace(name))
            throw new InvalidOperationException("Environment variable name is missing.");
        return Environment.GetEnvironmentVariable(name)
            ?? throw new InvalidOperationException($"Environment variable is missing: {name}");
    }

    private static JsonElement SelectById(
        JsonElement value,
        string id,
        string path,
        string description
    )
    {
        List<JsonElement> matches = [];
        int index = 0;

        foreach (JsonElement element in RequireArray(value, path))
        {
            JsonElement item = RequireObject(element, $"{path}[{index}]");
            string itemId = RequireString(
                RequireProperty(item, "Id", $"{path}[{index}]"),
                $"{path}[{index}].Id"
            );
            if (itemId.Equals(id, StringComparison.Ordinal))
                matches.Add(item);
            index++;
        }
        if (matches.Count != 1)
            throw new InvalidOperationException(
                $"Expected exactly one {description} with Id '{id}'; found {matches.Count}."
            );
        return matches[0];
    }

    private static JsonElement RequireProperty(JsonElement value, string name, string path)
    {
        JsonElement match = default;
        int count = 0;

        foreach (JsonProperty property in RequireObject(value, path).EnumerateObject())
        {
            if (property.Name.Equals(name, StringComparison.OrdinalIgnoreCase))
            {
                match = property.Value;
                count++;
            }
        }
        if (count != 1)
            throw new InvalidOperationException(
                $"{path}.{name} must occur exactly once; found {count}."
            );
        return match;
    }

    private static JsonElement RequireObject(JsonElement value, string path)
    {
        if (value.ValueKind != JsonValueKind.Object)
            throw new InvalidOperationException($"{path} must be an object.");
        return value;
    }

    private static JsonElement.ArrayEnumerator RequireArray(JsonElement value, string path)
    {
        if (value.ValueKind != JsonValueKind.Array)
            throw new InvalidOperationException($"{path} must be an array.");
        return value.EnumerateArray();
    }

    private static string RequireString(JsonElement value, string path)
    {
        if (value.ValueKind != JsonValueKind.String)
            throw new InvalidOperationException($"{path} must be a string.");
        return value.GetString()!;
    }

    private static int RequireInt32(JsonElement value, string path)
    {
        if (value.ValueKind != JsonValueKind.Number || !value.TryGetInt32(out int result))
            throw new InvalidOperationException($"{path} must be a 32-bit integer.");
        return result;
    }

    private static bool ReadOptionalBoolean(
        JsonElement value,
        string name,
        bool defaultValue,
        string path
    )
    {
        JsonProperty? match = null;

        foreach (JsonProperty property in RequireObject(value, path).EnumerateObject())
        {
            if (!property.Name.Equals(name, StringComparison.OrdinalIgnoreCase))
                continue;
            if (match.HasValue)
                throw new InvalidOperationException($"{path}.{name} must occur at most once.");
            match = property;
        }
        if (!match.HasValue)
            return defaultValue;
        if (match.Value.Value.ValueKind is not (JsonValueKind.True or JsonValueKind.False))
            throw new InvalidOperationException($"{path}.{name} must be a boolean.");
        return match.Value.Value.GetBoolean();
    }

    private static Dictionary<string, string> ReadStringDictionary(JsonElement value, string path)
    {
        Dictionary<string, string> result = new(StringComparer.Ordinal);

        foreach (JsonProperty property in RequireObject(value, path).EnumerateObject())
        {
            if (
                !result.TryAdd(
                    property.Name,
                    RequireString(property.Value, $"{path}.{property.Name}")
                )
            )
                throw new InvalidOperationException(
                    $"{path} contains duplicate key '{property.Name}'."
                );
        }
        return result;
    }

    private static List<string> ReadStringArray(JsonElement value, string path)
    {
        List<string> result = [];
        int index = 0;

        foreach (JsonElement element in RequireArray(value, path))
            result.Add(RequireString(element, $"{path}[{index++}]"));
        return result;
    }

    private static List<int> ReadInt32Array(JsonElement value, string path)
    {
        List<int> result = [];
        int index = 0;

        foreach (JsonElement element in RequireArray(value, path))
            result.Add(RequireInt32(element, $"{path}[{index++}]"));
        return result;
    }

    private static async Task<string> DiscoverNamespace(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        string storeId,
        string cacheId,
        int timeout,
        CancellationToken token
    )
    {
        await using NpgsqlCommand command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandTimeout = timeout;
        command.CommandText =
            "SELECT DISTINCT namespace FROM media_cache_primary_entries WHERE namespace LIKE @prefix ESCAPE '\\' ORDER BY namespace";
        command.Parameters.AddWithValue(
            "prefix",
            $"backup:media-cache:{EscapeLike(storeId)}:{EscapeLike(cacheId)}|primary|%"
        );
        List<string> namespaces = [];
        await using NpgsqlDataReader reader = await command.ExecuteReaderAsync(token);

        while (await reader.ReadAsync(token))
            namespaces.Add(reader.GetString(0));
        if (namespaces.Count != 1)
            throw new InvalidOperationException(
                $"Expected exactly one primary cache namespace; found {namespaces.Count}."
            );
        return namespaces[0];
    }

    private static long? NullableInt64(NpgsqlDataReader reader, int ordinal) =>
        reader.IsDBNull(ordinal) ? null : reader.GetInt64(ordinal);

    private static int? NullableInt32(NpgsqlDataReader reader, int ordinal) =>
        reader.IsDBNull(ordinal) ? null : reader.GetInt32(ordinal);

    private static async Task SetReadOnly(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        int timeout,
        CancellationToken token
    )
    {
        await using NpgsqlCommand command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandTimeout = timeout;
        command.CommandText = "SET TRANSACTION READ ONLY";
        await command.ExecuteNonQueryAsync(token);
    }

    private static bool TryParseVideo(string path, out string group, out int width, out int height)
    {
        Match match = VideoPathPattern.Match(path);
        if (!match.Success)
        {
            group = string.Empty;
            width = height = 0;
            return false;
        }
        group = $"{match.Groups["post"].Value}/{match.Groups["media"].Value}";
        width = int.Parse(match.Groups["width"].Value);
        height = int.Parse(match.Groups["height"].Value);
        return true;
    }

    private static StringComparer PathComparer =>
        OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;

    private static StringComparison PathComparison =>
        OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;

    internal static bool IsFullyQualifiedPath(string path, bool? windows = null) =>
        (windows ?? OperatingSystem.IsWindows())
            ? Regex.IsMatch(path, @"^[A-Za-z]:[\\/]", RegexOptions.CultureInvariant)
                || path.StartsWith("\\\\", StringComparison.Ordinal)
            : path.StartsWith("/", StringComparison.Ordinal);

    private static string EscapeLike(string value) =>
        value
            .Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("%", "\\%", StringComparison.Ordinal)
            .Replace("_", "\\_", StringComparison.Ordinal);

    private static string RequireAbsolutePath(string path, string name)
    {
        if (!IsFullyQualifiedPath(path))
            throw new ArgumentException("Path must be fully qualified.", name);
        return Path.GetFullPath(path);
    }

    private static void ValidateDbRow(
        string path,
        int? partitionId,
        long? size,
        Dictionary<string, List<VideoVariantLooseCacheRow>> rows,
        List<string> errors
    )
    {
        if (
            !rows.TryGetValue(path, out List<VideoVariantLooseCacheRow>? matches)
            || matches.Count != 1
        )
        {
            errors.Add($"{path}: expected exactly one exact DB row.");
            return;
        }
        VideoVariantLooseCacheRow row = matches[0];
        if (partitionId.HasValue && row.PartitionId != partitionId)
            errors.Add($"{path}: DB partition changed.");
        if (size.HasValue && row.FileSize != size)
            errors.Add($"{path}: DB file_size differs from original fingerprint.");
    }

    internal static void AddTargetDbRow(
        HashSet<string> targetPaths,
        Dictionary<string, List<VideoVariantLooseCacheRow>> rows,
        VideoVariantLooseCacheRow row
    )
    {
        string path = row.Path.Replace('\\', '/');
        if (!targetPaths.Contains(path))
            return;
        if (!rows.TryGetValue(path, out List<VideoVariantLooseCacheRow>? matches))
            rows[path] = matches = [];
        matches.Add(row with { Path = path });
    }

    private static VideoVariantLooseCleanupPreflightResult Result(
        VideoVariantLooseCleanupManifest manifest,
        List<string> errors,
        int originalFiles = 0,
        long originalBytes = 0,
        int directFiles = 0,
        long directBytes = 0
    ) =>
        new()
        {
            IsValid = errors.Count == 0,
            Errors = errors,
            Operations = manifest.Operations.Count,
            OriginalFiles = originalFiles,
            OriginalBytes = originalBytes,
            DirectFiles = directFiles,
            DirectBytes = directBytes,
        };

    private static void ValidateManifest(VideoVariantLooseCleanupManifest manifest, bool verifyHash)
    {
        if (manifest.Version != 1)
            throw new InvalidOperationException("Loose cleanup manifest version must be 1.");
        ValidateFileOptions(manifest.FileParallelism, manifest.SelectionLimit);
        RequireAbsolutePath(manifest.DataConfigPath, nameof(manifest.DataConfigPath));
        RequireAbsolutePath(manifest.DirectRoot, nameof(manifest.DirectRoot));
        if (manifest.Paths.Count != manifest.Paths.Distinct(StringComparer.Ordinal).Count())
            throw new InvalidOperationException("Manifest path table contains duplicates.");

        foreach (VideoVariantLooseCleanupOperation operation in manifest.Operations)
        {
            if (
                operation.RemovePathIndex < 0
                || operation.RemovePathIndex >= manifest.Paths.Count
                || operation.KeepPathIndex < 0
                || operation.KeepPathIndex >= manifest.Paths.Count
            )
                throw new InvalidOperationException("Manifest path index is out of range.");
        }
        if (
            verifyHash
            && !CalculateApprovalHash(manifest)
                .Equals(manifest.ApprovalHash, StringComparison.OrdinalIgnoreCase)
        )
            throw new InvalidOperationException("Loose cleanup manifest approval hash is invalid.");
    }

    private static void ValidateTimeout(int value)
    {
        if (value <= 0)
            throw new ArgumentOutOfRangeException(nameof(value));
    }

    private static void ValidateFileOptions(int fileParallelism, int? fileLimit)
    {
        if (fileParallelism <= 0)
            throw new ArgumentOutOfRangeException(nameof(fileParallelism));
        if (fileLimit <= 0)
            throw new ArgumentOutOfRangeException(nameof(fileLimit));
    }

    private static void ValidateSelectionKind(string value)
    {
        if (value is not ("video-variants" or "gif-thumbs" or "video-thumbs" or "photo-variants"))
            throw new ArgumentException($"Unknown loose cleanup selection: {value}");
    }

    private static string GetPreflightRowsSql(VideoVariantLooseCleanupManifest manifest)
    {
        string[] paths = manifest
            .Operations.SelectMany(operation =>
                new[]
                {
                    manifest.Paths[operation.RemovePathIndex],
                    manifest.Paths[operation.KeepPathIndex],
                }
            )
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        if (
            paths.Length > 0
            && paths.All(path => GifThumbPathPattern.IsMatch(path.Replace('\\', '/')))
        )
            return GifThumbRowsSql;
        if (
            paths.Length > 0
            && paths.All(path => VideoThumbPathPattern.IsMatch(path.Replace('\\', '/')))
        )
            return VideoThumbRowsSql;
        if (
            paths.Length > 0
            && paths.All(path => PhotoPathPattern.IsMatch(path.Replace('\\', '/')))
        )
            return PhotoRowsSql;
        return PreflightRowsSql;
    }

    private sealed record ParsedRow(
        VideoVariantLooseCacheRow Row,
        string Group,
        int Width,
        int Height
    );

    private sealed record ParsedThumbRow(
        VideoVariantLooseCacheRow Row,
        string Group,
        string Format,
        string ResolutionType,
        string Name
    );

    private sealed record ParsedPartition(bool Enabled, List<string> Paths);

    private sealed record PendingOperation(
        string Remove,
        string Keep,
        int PartitionId,
        int KeepPartitionId,
        long OriginalSize,
        long OriginalLastWriteUtcTicks,
        bool DirectPresent,
        long DirectSize,
        long DirectLastWriteUtcTicks
    );

    private sealed record SelectionCheckResult(
        PendingOperation? Operation,
        List<VideoVariantLooseCleanupIssue> Issues
    );

    private sealed record KeepCheck(string Path, string RootKey);

    private sealed record RemoveCheck(
        int Index,
        string Path,
        string Keep,
        VideoVariantLooseCleanupOperation Operation,
        string RootKey
    );

    private sealed record RootWork(string Key, string Label)
    {
        public List<KeepCheck> Keeps { get; } = [];
        public List<RemoveCheck> Removes { get; } = [];
    }

    private sealed record KeepFileCheck(string Path, bool Exists);

    private sealed record FileCheck(int Index, bool Exists, long Size, long LastWriteUtcTicks);

    private sealed record RootResult(
        string Root,
        List<KeepFileCheck> Keeps,
        List<FileCheck> Removes,
        List<VideoVariantLooseCleanupIssue> Issues
    )
    {
        public int OriginalFiles => Removes.Count(value => value.Exists);
        public long OriginalBytes => Removes.Where(value => value.Exists).Sum(value => value.Size);
    }

    private sealed record DirectResult(Dictionary<int, FileCheck> Files);
}

internal sealed record ResolvedConfig(
    string DataConfigPath,
    long DataConfigLength,
    long DataConfigLastWriteUtcTicks,
    string MediaStoreId,
    string CacheId,
    string ConnectionString,
    Dictionary<int, string> MediaRoots,
    List<VideoVariantLooseCleanupIssue> Issues
);

internal sealed record VideoVariantLooseCacheRow(
    string Path,
    long? StreamSize,
    long? FileSize,
    int? PartitionId
);

internal sealed record VideoVariantLooseSelection(
    VideoVariantLooseCacheRow Remove,
    VideoVariantLooseCacheRow Keep
);

public sealed record VideoVariantLooseCleanupManifest
{
    public int Version { get; init; }
    public DateTimeOffset CreatedUtc { get; init; }
    public string DataConfigPath { get; init; } = string.Empty;
    public long DataConfigLength { get; init; }
    public long DataConfigLastWriteUtcTicks { get; init; }
    public string MediaStoreId { get; init; } = string.Empty;
    public string CacheId { get; init; } = string.Empty;
    public string CacheNamespace { get; init; } = string.Empty;
    public string DirectRoot { get; init; } = string.Empty;
    public long CacheRowsScanned { get; init; }
    public long VideoEntries { get; init; }
    public int FileParallelism { get; init; }
    public int? SelectionLimit { get; init; }
    public bool PhysicalChecked { get; init; }
    public List<string> Paths { get; init; } = [];
    public List<VideoVariantLooseCleanupOperation> Operations { get; init; } = [];
    public List<VideoVariantLooseCleanupIssue> Issues { get; init; } = [];
    public string ApprovalHash { get; set; } = string.Empty;
}

public sealed class VideoVariantLooseCleanupOperation
{
    public int RemovePathIndex { get; init; }
    public int KeepPathIndex { get; init; }
    public int PartitionId { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? KeepPartitionId { get; init; }
    public long OriginalSize { get; init; }
    public long OriginalLastWriteUtcTicks { get; init; }
    public bool DirectPresent { get; init; }
    public long DirectSize { get; init; }
    public long DirectLastWriteUtcTicks { get; init; }
}

public sealed record VideoVariantLooseCleanupIssue(string Path, string Code, string Message);

public sealed record VideoVariantLooseCleanupProgress(
    string Phase,
    long CacheRowsScanned,
    long VideoEntries,
    int ItemsChecked,
    int TotalItems,
    int Issues,
    TimeSpan Elapsed = default,
    string? RootLabel = null,
    int RootChecked = 0,
    int RootTotal = 0
);

public sealed record VideoVariantLooseCleanupPreflightResult
{
    public bool IsValid { get; init; }
    public List<string> Errors { get; init; } = [];
    public int Operations { get; init; }
    public int OriginalFiles { get; init; }
    public long OriginalBytes { get; init; }
    public int DirectFiles { get; init; }
    public long DirectBytes { get; init; }
    public VideoVariantLooseCleanupManifest? PhysicalManifest { get; init; }
}
