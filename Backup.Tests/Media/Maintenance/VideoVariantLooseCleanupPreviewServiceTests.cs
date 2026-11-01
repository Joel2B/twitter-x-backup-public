using Backup.Infrastructure.Media.Maintenance;

namespace Backup.Tests;

public sealed class VideoVariantLooseCleanupPreviewServiceTests : IDisposable
{
    private const string Low = "post/video/media/mp4/low/320x180.mp4";
    private const string Medium = "post/video/media/mp4/medium/640x360.mp4";
    private const string Wide = "post/video/media/mp4/wide/1920x400.mp4";
    private const string High = "post/video/media/mp4/high/1280x720.mp4";
    private readonly string _root = Path.Combine(
        Path.GetTempPath(),
        $"loose-preview-{Guid.NewGuid():N}"
    );

    [Fact]
    public void SelectionSql_IsReadOnlyAndUsesPostgresRanking()
    {
        string sql = VideoVariantLooseCleanupPreviewService.SelectionSql;

        Assert.DoesNotContain("DELETE", sql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("UPDATE", sql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("INSERT", sql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("CREATE", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("regexp_match", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("row_number() OVER", sql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("withnormalized", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("first_value", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("replace(path, chr(92), '/')", sql, StringComparison.Ordinal);
        Assert.Contains("namespace = @namespace", sql, StringComparison.Ordinal);
    }

    [Fact]
    public void PreflightRowsSql_IsReadOnlyParsedQueryWithoutRanking()
    {
        string sql = VideoVariantLooseCleanupPreviewService.PreflightRowsSql;

        Assert.Contains("replace(path, chr(92), '/')", sql, StringComparison.Ordinal);
        Assert.Contains("FROM parsed", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("namespace = @namespace", sql, StringComparison.Ordinal);
        Assert.DoesNotContain("ranked", sql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("DELETE", sql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("UPDATE", sql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("INSERT", sql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("CREATE", sql, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void AddTargetDbRow_NormalizesBackslashesAndRetainsDuplicates()
    {
        HashSet<string> targets = [Low];
        Dictionary<string, List<VideoVariantLooseCacheRow>> rows = new(StringComparer.Ordinal);

        VideoVariantLooseCleanupPreviewService.AddTargetDbRow(
            targets,
            rows,
            new(Low.Replace('/', '\\'), 1, 2, 3)
        );
        VideoVariantLooseCleanupPreviewService.AddTargetDbRow(targets, rows, new(Low, 4, 5, 6));
        VideoVariantLooseCleanupPreviewService.AddTargetDbRow(targets, rows, new(High, 7, 8, 9));

        List<VideoVariantLooseCacheRow> matches = Assert.Single(rows).Value;
        Assert.Equal(2, matches.Count);
        Assert.All(matches, row => Assert.Equal(Low, row.Path));
    }

    [Fact]
    public void SelectCandidates_SelectsOnlyDominatedAndExcludesGif()
    {
        List<VideoVariantLooseCleanupIssue> issues = [];
        List<VideoVariantLooseSelection> result =
            VideoVariantLooseCleanupPreviewService.SelectCandidates(
                [
                    new VideoVariantLooseCacheRow(Low, 1, 1, 1),
                    new VideoVariantLooseCacheRow(Wide, 1, 1, 1),
                    new VideoVariantLooseCacheRow(High, 1, 1, 1),
                    new VideoVariantLooseCacheRow(
                        "post/gif/media/mp4/gif-source/index.mp4",
                        1,
                        1,
                        1
                    ),
                    new VideoVariantLooseCacheRow(
                        "post/video/media-2/mp4/gif-source/160x90.mp4",
                        1,
                        1,
                        1
                    ),
                ],
                issues
            );

        Assert.Single(result);
        VideoVariantLooseSelection selected = result[0];
        Assert.Equal(Low, selected.Remove.Path);
        Assert.Equal(High, selected.Keep.Path);
        Assert.Empty(issues);
    }

    [Fact]
    public void SelectGifThumbCandidates_JpgKeepsOnlyOrig()
    {
        const string root = "post/gif/media/thumb/thumb";
        VideoVariantLooseCacheRow[] rows =
        [
            Row($"{root}/jpg/dimension/120x120.jpg"),
            Row($"{root}/jpg/dimension/240x240.jpg"),
            Row($"{root}/jpg/dimension/360x360.jpg"),
            Row($"{root}/jpg/size/large.jpg"),
            Row($"{root}/jpg/size/medium.jpg"),
            Row($"{root}/jpg/size/orig.jpg"),
            Row($"{root}/jpg/size/small.jpg"),
        ];
        List<VideoVariantLooseCleanupIssue> issues = [];

        List<VideoVariantLooseSelection> result =
            VideoVariantLooseCleanupPreviewService.SelectGifThumbCandidates(rows, issues);

        Assert.Equal(6, result.Count);
        Assert.All(result, item => Assert.Equal($"{root}/jpg/size/orig.jpg", item.Keep.Path));
        Assert.DoesNotContain(result, item => item.Remove.Path.EndsWith("/orig.jpg"));
        Assert.Empty(issues);
    }

    [Fact]
    public void SelectGifThumbCandidates_PngKeepsPngOrigAndJpgLarge()
    {
        const string root = "post/gif/media/thumb/thumb";
        VideoVariantLooseCacheRow[] rows =
        [
            Row($"{root}/png/size/orig.png"),
            Row($"{root}/png/size/large.png"),
            Row($"{root}/jpg/size/orig.jpg"),
            Row($"{root}/jpg/size/large.jpg"),
            Row($"{root}/jpg/size/small.jpg"),
        ];
        List<VideoVariantLooseCleanupIssue> issues = [];

        List<VideoVariantLooseSelection> result =
            VideoVariantLooseCleanupPreviewService.SelectGifThumbCandidates(rows, issues);

        Assert.Equal(3, result.Count);
        Assert.DoesNotContain(result, item => item.Remove.Path == $"{root}/png/size/orig.png");
        Assert.DoesNotContain(result, item => item.Remove.Path == $"{root}/jpg/size/large.jpg");
        Assert.Contains(result, item => item.Keep.Path == $"{root}/png/size/orig.png");
        Assert.Contains(result, item => item.Keep.Path == $"{root}/jpg/size/large.jpg");
        Assert.Empty(issues);
    }

    [Fact]
    public void SelectGifThumbCandidates_MissingOrDuplicateKeepBlocksGroup()
    {
        const string root = "post/gif/media/thumb/thumb";
        List<VideoVariantLooseCleanupIssue> issues = [];

        List<VideoVariantLooseSelection> result =
            VideoVariantLooseCleanupPreviewService.SelectGifThumbCandidates(
                [Row($"{root}/jpg/size/large.jpg"), Row($"{root}/jpg/size/small.jpg")],
                issues
            );

        Assert.Empty(result);
        Assert.Contains(issues, issue => issue.Code == "keep-set-invalid");
    }

    [Fact]
    public void GifThumbRowsSql_IsReadOnlyAndNamespaceScoped()
    {
        string sql = VideoVariantLooseCleanupPreviewService.GifThumbRowsSql;

        Assert.Contains("/gif/", sql, StringComparison.Ordinal);
        Assert.Contains("namespace = @namespace", sql, StringComparison.Ordinal);
        Assert.DoesNotContain("DELETE", sql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("UPDATE", sql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("INSERT", sql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("CREATE", sql, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void SelectVideoThumbCandidates_JpgKeepsOnlyOrig()
    {
        const string root = "post/video/media/thumb/thumb";
        VideoVariantLooseCacheRow[] rows =
        [
            Row($"{root}/jpg/dimension/120x120.jpg"),
            Row($"{root}/jpg/dimension/240x240.jpg"),
            Row($"{root}/jpg/dimension/360x360.jpg"),
            Row($"{root}/jpg/size/large.jpg"),
            Row($"{root}/jpg/size/medium.jpg"),
            Row($"{root}/jpg/size/orig.jpg"),
            Row($"{root}/jpg/size/small.jpg"),
        ];
        List<VideoVariantLooseCleanupIssue> issues = [];

        List<VideoVariantLooseSelection> result =
            VideoVariantLooseCleanupPreviewService.SelectVideoThumbCandidates(rows, issues);

        Assert.Equal(6, result.Count);
        Assert.All(result, item => Assert.Equal($"{root}/jpg/size/orig.jpg", item.Keep.Path));
        Assert.Empty(issues);
    }

    [Fact]
    public void SelectVideoThumbCandidates_PngKeepsPngOrigAndJpgLarge()
    {
        const string root = "post/video/media/thumb/thumb";
        List<VideoVariantLooseCleanupIssue> issues = [];

        List<VideoVariantLooseSelection> result =
            VideoVariantLooseCleanupPreviewService.SelectVideoThumbCandidates(
                [
                    Row($"{root}/png/size/orig.png"),
                    Row($"{root}/png/size/large.png"),
                    Row($"{root}/jpg/size/orig.jpg"),
                    Row($"{root}/jpg/size/large.jpg"),
                    Row($"{root}/jpg/size/small.jpg"),
                ],
                issues
            );

        Assert.Equal(3, result.Count);
        Assert.DoesNotContain(result, item => item.Remove.Path == $"{root}/png/size/orig.png");
        Assert.DoesNotContain(result, item => item.Remove.Path == $"{root}/jpg/size/large.jpg");
        Assert.Empty(issues);
    }

    [Fact]
    public void SelectVideoThumbCandidates_IncompletePngIsNotReclassifiedAsJpg()
    {
        const string root = "post/video/media/thumb/thumb";
        List<VideoVariantLooseCleanupIssue> issues = [];

        List<VideoVariantLooseSelection> result =
            VideoVariantLooseCleanupPreviewService.SelectVideoThumbCandidates(
                [
                    Row($"{root}/png/size/large.png"),
                    Row($"{root}/jpg/size/orig.jpg"),
                    Row($"{root}/jpg/size/large.jpg"),
                ],
                issues
            );

        Assert.Empty(result);
        Assert.Single(issues, issue => issue.Code == "keep-set-invalid");
    }

    [Fact]
    public void SelectVideoThumbCandidates_UnexpectedPathInvalidatesWholeGroup()
    {
        const string root = "post/video/media/thumb/thumb";
        List<VideoVariantLooseCleanupIssue> issues = [];

        List<VideoVariantLooseSelection> result =
            VideoVariantLooseCleanupPreviewService.SelectVideoThumbCandidates(
                [
                    Row($"{root}/png/size/orig.jpeg"),
                    Row($"{root}/jpg/size/orig.jpg"),
                    Row($"{root}/jpg/size/large.jpg"),
                    Row($"{root}/jpg/size/small.jpg"),
                ],
                issues
            );

        Assert.Empty(result);
        Assert.Contains(issues, issue => issue.Code == "unexpected-thumbnail-path");
    }

    [Fact]
    public void VideoThumbRowsSql_IsReadOnlyAndNamespaceScoped()
    {
        string sql = VideoVariantLooseCleanupPreviewService.VideoThumbRowsSql;

        Assert.Contains("/video/", sql, StringComparison.Ordinal);
        Assert.Contains("/thumb/", sql, StringComparison.Ordinal);
        Assert.Contains("namespace = @namespace", sql, StringComparison.Ordinal);
        Assert.DoesNotContain("DELETE", sql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("UPDATE", sql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("INSERT", sql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("CREATE", sql, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void PartitionAliasAndContainment_ResolveWithoutGuessing()
    {
        string aliasRoot = OperatingSystem.IsWindows() ? "L:" : "/mnt/l";
        string? root = VideoVariantLooseCleanupPreviewService.ResolvePartitionRoot(
            ["@L", "apps"],
            new Dictionary<string, string> { ["L"] = aliasRoot }
        );

        string expectedRoot = OperatingSystem.IsWindows()
            ? Path.GetFullPath("L:\\apps")
            : Path.GetFullPath("/mnt/l/apps");
        Assert.Equal(expectedRoot, root);
        Assert.Null(
            VideoVariantLooseCleanupPreviewService.ResolvePartitionRoot(
                ["data"],
                new Dictionary<string, string>()
            )
        );
        Assert.Throws<InvalidOperationException>(
            () =>
                VideoVariantLooseCleanupPreviewService.ResolveContainedPath(
                    Path.Combine(Path.GetPathRoot(_root)!, "media"),
                    "../escape.mp4"
                )
        );
    }

    [Theory]
    [InlineData(@"L:\apps\twitter-x-backup", true, true)]
    [InlineData(@"\\server\backup\direct", true, true)]
    [InlineData("/mnt/l/apps/twitter-x-backup", false, true)]
    [InlineData("/mnt/s/backup/direct", false, true)]
    [InlineData("/mnt/l", true, false)]
    [InlineData(@"L:\apps", false, false)]
    [InlineData("relative/path", true, false)]
    [InlineData("relative/path", false, false)]
    public void IsFullyQualifiedPath_UsesSelectedPlatformSyntax(
        string path,
        bool windows,
        bool expected
    )
    {
        Assert.Equal(
            expected,
            VideoVariantLooseCleanupPreviewService.IsFullyQualifiedPath(path, windows)
        );
    }

    [Fact]
    public void LoadConfig_IgnoresIncompleteLegacyAndIrrelevantSections()
    {
        Directory.CreateDirectory(_root);
        string configPath = Path.Combine(_root, "Data.json");
        File.WriteAllText(
            configPath,
            """
            {
              "Aliases": { "D": "D:" },
              "Partitions": [
                { "Id": 7, "Paths": ["@D", "archive"] }
              ],
              "Post": [{}],
              "Dump": [{ "Paths": {} }],
              "Bulk": "legacy-invalid",
              "Backup": null,
              "Media": [
                {
                  "Id": "other",
                  "Enabled": false,
                  "LegacyShape": true
                },
                {
                  "Id": "store",
                  "Cache": [
                    {
                      "Id": "cache",
                      "Type": "postgres",
                      "ConnectionString": "Host=localhost;Database=test"
                    }
                  ],
                  "Paths": { "Media": { "Paths": ["media"] } },
                  "Partitions": [7]
                }
              ]
            }
            """
        );

        ResolvedConfig config = VideoVariantLooseCleanupPreviewService.LoadConfig(
            configPath,
            "store",
            "cache"
        );

        FileInfo info = new(configPath);
        Assert.Equal(Path.GetFullPath(configPath), config.DataConfigPath);
        Assert.Equal(info.Length, config.DataConfigLength);
        Assert.Equal(info.LastWriteTimeUtc.Ticks, config.DataConfigLastWriteUtcTicks);
        Assert.Equal("Host=localhost;Database=test", config.ConnectionString);
        if (OperatingSystem.IsWindows())
        {
            Assert.Equal(Path.GetFullPath("D:\\archive\\media"), config.MediaRoots[7]);
            Assert.Empty(config.Issues);
        }
        else
            Assert.Contains(config.Issues, issue => issue.Code == "partition-unresolved");
    }

    [Fact]
    public void BuildManifest_ReportsStaleOriginalAndOmitsOperation()
    {
        Directory.CreateDirectory(_root);
        string mediaRoot = Path.Combine(_root, "media");
        Directory.CreateDirectory(
            Path.GetDirectoryName(
                Path.Combine(mediaRoot, High.Replace('/', Path.DirectorySeparatorChar))
            )!
        );
        File.WriteAllText(
            Path.Combine(mediaRoot, High.Replace('/', Path.DirectorySeparatorChar)),
            "keep"
        );
        ResolvedConfig config = Config(mediaRoot);

        VideoVariantLooseCleanupManifest manifest =
            VideoVariantLooseCleanupPreviewService.BuildManifest(
                config,
                "namespace",
                Path.Combine(_root, "direct"),
                [new(Low, 1, 1, 1), new(High, 1, 1, 1)],
                2,
                2
            );

        Assert.Empty(manifest.Operations);
        Assert.True(manifest.PhysicalChecked);
        Assert.Contains(manifest.Issues, issue => issue.Path == Low && issue.Code == "cache-stale");
    }

    [Fact]
    public void ManifestHash_IsDeterministicAndRejectsTampering()
    {
        Directory.CreateDirectory(_root);
        string configPath = Path.Combine(_root, "Data.json");
        File.WriteAllText(configPath, "{}");
        ResolvedConfig config = Config(Path.Combine(_root, "media"), configPath);
        DateTimeOffset created = new(2026, 1, 2, 3, 4, 5, TimeSpan.Zero);
        VideoVariantLooseCleanupManifest first =
            VideoVariantLooseCleanupPreviewService.BuildManifest(
                config,
                "namespace",
                Path.Combine(_root, "direct"),
                [],
                0,
                0,
                createdUtc: created
            );
        VideoVariantLooseCleanupManifest second =
            VideoVariantLooseCleanupPreviewService.BuildManifest(
                config,
                "namespace",
                Path.Combine(_root, "direct"),
                [],
                0,
                0,
                createdUtc: created
            );
        VideoVariantLooseCleanupPreviewService service = new();
        string path = Path.Combine(_root, "manifest.json");

        Assert.Equal(first.ApprovalHash, second.ApprovalHash);
        service.SaveManifest(first, path);
        Assert.Equal(first.ApprovalHash, service.LoadManifest(path).ApprovalHash);
        string json = File.ReadAllText(path)
            .Replace("\"CacheId\": \"cache\"", "\"CacheId\": \"tampered\"");
        File.WriteAllText(path, json);
        Assert.Throws<InvalidOperationException>(() => service.LoadManifest(path));
    }

    [Fact]
    public void BuildManifest_DbOnlyUsesCacheWithoutPhysicalFiles()
    {
        Directory.CreateDirectory(_root);
        ResolvedConfig config = Config(Path.Combine(_root, "absent-media"));

        VideoVariantLooseCleanupManifest manifest =
            VideoVariantLooseCleanupPreviewService.BuildManifest(
                config,
                "namespace",
                Path.Combine(_root, "absent-direct"),
                [new(Low, 1, 123, 1), new(High, 1, 456, 1)],
                2,
                2,
                dbOnly: true
            );

        VideoVariantLooseCleanupOperation operation = Assert.Single(manifest.Operations);
        Assert.False(manifest.PhysicalChecked);
        Assert.Equal(123, operation.OriginalSize);
        Assert.Equal(0, operation.OriginalLastWriteUtcTicks);
        Assert.False(operation.DirectPresent);
        Assert.Equal(0, operation.DirectSize);
        Assert.Equal(0, operation.DirectLastWriteUtcTicks);
        Assert.Empty(manifest.Issues);
    }

    [Fact]
    public void BuildManifest_DbOnlyOmitsNullCacheSize()
    {
        Directory.CreateDirectory(_root);
        ResolvedConfig config = Config(Path.Combine(_root, "absent-media"));

        VideoVariantLooseCleanupManifest manifest =
            VideoVariantLooseCleanupPreviewService.BuildManifest(
                config,
                "namespace",
                Path.Combine(_root, "absent-direct"),
                [new(Low, 1, null, 1), new(High, 1, 456, 1)],
                2,
                2,
                dbOnly: true
            );

        Assert.Empty(manifest.Operations);
        Assert.Contains(
            manifest.Issues,
            issue => issue.Path == Low && issue.Code == "cache-size-null"
        );
    }

    [Fact]
    public void ManifestHash_CoversPhysicalChecked()
    {
        Directory.CreateDirectory(_root);
        VideoVariantLooseCleanupManifest manifest =
            VideoVariantLooseCleanupPreviewService.BuildManifest(
                Config(Path.Combine(_root, "absent-media")),
                "namespace",
                Path.Combine(_root, "absent-direct"),
                [],
                0,
                0,
                dbOnly: true
            );
        VideoVariantLooseCleanupPreviewService service = new();
        string path = Path.Combine(_root, "manifest-physical.json");
        service.SaveManifest(manifest, path);

        string json = File.ReadAllText(path)
            .Replace("\"PhysicalChecked\": false", "\"PhysicalChecked\": true");
        File.WriteAllText(path, json);

        Assert.Throws<InvalidOperationException>(() => service.LoadManifest(path));
    }

    [Fact]
    public void BuildManifest_ParallelMatchesSequentialSemantics()
    {
        (ResolvedConfig config, VideoVariantLooseCacheRow[] rows) = PrepareCandidates();
        DateTimeOffset created = new(2026, 1, 2, 3, 4, 5, TimeSpan.Zero);

        VideoVariantLooseCleanupManifest sequential = Build(config, rows, 1, created);
        VideoVariantLooseCleanupManifest parallel = Build(config, rows, 4, created);

        Assert.Equal(sequential.Paths, parallel.Paths);
        Assert.Equal(OperationValues(sequential), OperationValues(parallel));
        Assert.Equal(sequential.Issues, parallel.Issues);
        Assert.NotEqual(sequential.ApprovalHash, parallel.ApprovalHash);
    }

    [Fact]
    public void BuildManifest_ParallelHashIsDeterministic()
    {
        (ResolvedConfig config, VideoVariantLooseCacheRow[] rows) = PrepareCandidates();
        DateTimeOffset created = new(2026, 1, 2, 3, 4, 5, TimeSpan.Zero);

        VideoVariantLooseCleanupManifest first = Build(config, rows, 4, created);
        VideoVariantLooseCleanupManifest second = Build(config, rows, 4, created);

        Assert.Equal(first.ApprovalHash, second.ApprovalHash);
    }

    [Fact]
    public void BuildManifest_FileLimitRestrictsConsideredSelections()
    {
        (ResolvedConfig config, VideoVariantLooseCacheRow[] rows) = PrepareCandidates();

        VideoVariantLooseCleanupManifest manifest =
            VideoVariantLooseCleanupPreviewService.BuildManifest(
                config,
                "namespace",
                Path.Combine(_root, "direct"),
                rows,
                rows.Length,
                rows.Length,
                fileParallelism: 4,
                fileLimit: 1
            );

        Assert.Equal(1, manifest.SelectionLimit);
        VideoVariantLooseCleanupOperation operation = Assert.Single(manifest.Operations);
        Assert.Equal(Low, manifest.Paths[operation.RemovePathIndex]);
    }

    [Fact]
    public void BuildManifest_PreselectedIsDeterministicAndLimited()
    {
        (ResolvedConfig config, VideoVariantLooseCacheRow[] rows) = PrepareCandidates();
        List<VideoVariantLooseSelection> selected = [new(rows[1], rows[2]), new(rows[0], rows[2])];
        DateTimeOffset created = new(2026, 1, 2, 3, 4, 5, TimeSpan.Zero);

        VideoVariantLooseCleanupManifest manifest =
            VideoVariantLooseCleanupPreviewService.BuildManifest(
                config,
                "namespace",
                Path.Combine(_root, "direct"),
                selected,
                [],
                99,
                3,
                fileParallelism: 2,
                fileLimit: 1,
                createdUtc: created
            );

        VideoVariantLooseCleanupOperation operation = Assert.Single(manifest.Operations);
        Assert.Equal(Low, manifest.Paths[operation.RemovePathIndex]);
        Assert.Equal(High, manifest.Paths[operation.KeepPathIndex]);
        Assert.Equal(99, manifest.CacheRowsScanned);
        Assert.Equal(3, manifest.VideoEntries);
        Assert.Equal(1, manifest.SelectionLimit);
    }

    [Fact]
    public async Task PhysicalBuilder_DoesNotCheckDirectAtThreshold()
    {
        Directory.CreateDirectory(_root);
        string mediaRoot = Path.Combine(_root, "media");
        WriteMedia(
            mediaRoot,
            Low,
            VideoVariantLooseCleanupPreviewService.DirectMinimumOriginalSize
        );
        WriteMedia(mediaRoot, High, 1);
        VideoVariantLooseCleanupManifest source = SourceManifest(
            "invalid\0direct-root",
            [(Low, High, 1, VideoVariantLooseCleanupPreviewService.DirectMinimumOriginalSize)]
        );

        VideoVariantLooseCleanupPreflightResult result =
            await VideoVariantLooseCleanupPreviewService.BuildPhysicalManifestAsync(
                source,
                new Dictionary<int, string> { [1] = mediaRoot },
                Rows((High, 1))
            );

        Assert.True(result.IsValid);
        Assert.False(Assert.Single(result.PhysicalManifest!.Operations).DirectPresent);
    }

    [Fact]
    public async Task PhysicalBuilder_DeduplicatesKeepCheck()
    {
        Directory.CreateDirectory(_root);
        string mediaRoot = Path.Combine(_root, "media");
        WriteMedia(mediaRoot, Low, 3);
        WriteMedia(mediaRoot, Medium, 4);
        WriteMedia(mediaRoot, High, 5);
        VideoVariantLooseCleanupManifest source = SourceManifest(
            Path.Combine(_root, "direct"),
            [(Low, High, 1, 3), (Medium, High, 1, 4)]
        );
        List<VideoVariantLooseCleanupProgress> values = [];

        VideoVariantLooseCleanupPreflightResult result =
            await VideoVariantLooseCleanupPreviewService.BuildPhysicalManifestAsync(
                source,
                new Dictionary<int, string> { [1] = mediaRoot },
                Rows((High, 1)),
                new InlineProgress<VideoVariantLooseCleanupProgress>(values.Add)
            );

        Assert.True(result.IsValid);
        Assert.Contains(values, value => value.RootLabel is not null && value.RootTotal == 3);
    }

    [Fact]
    public async Task PhysicalBuilder_OmitsCacheStaleWithoutFatalError()
    {
        Directory.CreateDirectory(_root);
        string mediaRoot = Path.Combine(_root, "media");
        WriteMedia(mediaRoot, High, 5);
        VideoVariantLooseCleanupManifest source = SourceManifest(
            Path.Combine(_root, "direct"),
            [(Low, High, 1, 3)]
        );

        VideoVariantLooseCleanupPreflightResult result =
            await VideoVariantLooseCleanupPreviewService.BuildPhysicalManifestAsync(
                source,
                new Dictionary<int, string> { [1] = mediaRoot },
                Rows((High, 1))
            );

        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
        Assert.Empty(result.PhysicalManifest!.Operations);
        Assert.Contains(
            result.PhysicalManifest.Issues,
            issue => issue.Path == Low && issue.Code == "cache-stale"
        );
    }

    [Fact]
    public async Task PhysicalBuilder_MissingKeepRemainsFatalWhenRemoveIsStale()
    {
        Directory.CreateDirectory(_root);
        string mediaRoot = Path.Combine(_root, "media");
        VideoVariantLooseCleanupManifest source = SourceManifest(
            Path.Combine(_root, "direct"),
            [(Low, High, 1, 3)]
        );

        VideoVariantLooseCleanupPreflightResult result =
            await VideoVariantLooseCleanupPreviewService.BuildPhysicalManifestAsync(
                source,
                new Dictionary<int, string> { [1] = mediaRoot },
                Rows((High, 1))
            );

        Assert.False(result.IsValid);
        Assert.Contains(
            result.Errors,
            error => error.Contains("Keep path is absent", StringComparison.Ordinal)
        );
        Assert.Contains(
            result.PhysicalManifest!.Issues,
            issue => issue.Path == Low && issue.Code == "cache-stale"
        );
        Assert.Contains(
            result.PhysicalManifest.Issues,
            issue => issue.Path == High && issue.Code == "keep-missing"
        );
    }

    [Fact]
    public async Task PhysicalBuilder_IsRootGroupedDeterministicAndSigned()
    {
        Directory.CreateDirectory(_root);
        string firstRoot = Path.Combine(_root, "L");
        string secondRoot = Path.Combine(_root, "S");
        WriteMedia(firstRoot, Low, 3);
        WriteMedia(secondRoot, Medium, 4);
        WriteMedia(secondRoot, High, 5);
        VideoVariantLooseCleanupManifest source = SourceManifest(
            Path.Combine(_root, "direct"),
            [(Medium, High, 2, 4), (Low, High, 1, 3)]
        );
        Dictionary<int, string> roots = new() { [1] = firstRoot, [2] = secondRoot };
        Dictionary<string, VideoVariantLooseCacheRow> rows = Rows((High, 2));

        VideoVariantLooseCleanupPreflightResult first =
            await VideoVariantLooseCleanupPreviewService.BuildPhysicalManifestAsync(
                source,
                roots,
                rows
            );
        VideoVariantLooseCleanupPreflightResult second =
            await VideoVariantLooseCleanupPreviewService.BuildPhysicalManifestAsync(
                source,
                roots,
                rows
            );

        Assert.True(first.IsValid);
        Assert.True(first.PhysicalManifest!.PhysicalChecked);
        Assert.Equal(1, first.PhysicalManifest.FileParallelism);
        Assert.Equal(first.PhysicalManifest.ApprovalHash, second.PhysicalManifest!.ApprovalHash);
        Assert.Equal(
            VideoVariantLooseCleanupPreviewService.CalculateApprovalHash(first.PhysicalManifest),
            first.PhysicalManifest.ApprovalHash
        );
        Assert.Equal(
            [Low, Medium],
            first.PhysicalManifest.Operations.Select(operation =>
                first.PhysicalManifest.Paths[operation.RemovePathIndex]
            )
        );
    }

    [Fact]
    public async Task PhysicalBuilder_GifThumbsEnumeratesGroupAndRetainsFingerprints()
    {
        const string keep = "post/gif/media/thumb/source/jpg/size/orig.jpg";
        const string remove = "post/gif/media/thumb/source/jpg/size/small.jpg";
        const string stale = "post/gif/media/thumb/source/jpg/dimension/120x120.jpg";
        Directory.CreateDirectory(_root);
        string mediaRoot = Path.Combine(_root, "media");
        WriteMedia(mediaRoot, keep, 5);
        WriteMedia(mediaRoot, remove, 3);
        VideoVariantLooseCleanupManifest source = SourceManifest(
            Path.Combine(_root, "direct"),
            [(remove, keep, 1, 3), (stale, keep, 1, 2)]
        ) with
        {
            FileParallelism = 4,
        };

        VideoVariantLooseCleanupPreflightResult result =
            await VideoVariantLooseCleanupPreviewService.BuildPhysicalManifestAsync(
                source,
                new Dictionary<int, string> { [1] = mediaRoot },
                Rows((keep, 1))
            );

        Assert.True(result.IsValid);
        Assert.Empty(result.PhysicalManifest!.Operations);
        Assert.Contains(
            result.PhysicalManifest.Issues,
            issue => issue.Path == stale && issue.Code == "cache-stale"
        );
    }

    [Fact]
    public async Task PhysicalBuilder_GifThumbsMatchesWindowsPathsIgnoringCase()
    {
        if (!OperatingSystem.IsWindows())
            return;
        const string keep = "post/GIF/media/THUMB/source/JPG/SIZE/ORIG.JPG";
        const string remove = "post/GIF/media/THUMB/source/JPG/SIZE/SMALL.JPG";
        Directory.CreateDirectory(_root);
        string mediaRoot = Path.Combine(_root, "media");
        WriteMedia(mediaRoot, keep.ToLowerInvariant(), 5);
        WriteMedia(mediaRoot, remove.ToLowerInvariant(), 3);
        VideoVariantLooseCleanupManifest source = SourceManifest(
            Path.Combine(_root, "direct"),
            [(remove, keep, 1, 3)]
        );

        VideoVariantLooseCleanupPreflightResult result =
            await VideoVariantLooseCleanupPreviewService.BuildPhysicalManifestAsync(
                source,
                new Dictionary<int, string> { [1] = mediaRoot },
                Rows((keep, 1))
            );

        Assert.True(result.IsValid);
        Assert.Single(result.PhysicalManifest!.Operations);
        Assert.Empty(result.PhysicalManifest.Issues);
    }

    private VideoVariantLooseCleanupManifest Build(
        ResolvedConfig config,
        VideoVariantLooseCacheRow[] rows,
        int fileParallelism,
        DateTimeOffset createdUtc
    ) =>
        VideoVariantLooseCleanupPreviewService.BuildManifest(
            config,
            "namespace",
            Path.Combine(_root, "direct"),
            rows,
            rows.Length,
            rows.Length,
            fileParallelism,
            createdUtc: createdUtc
        );

    private (ResolvedConfig Config, VideoVariantLooseCacheRow[] Rows) PrepareCandidates()
    {
        string mediaRoot = Path.Combine(_root, "media");

        foreach (string path in new[] { Low, Medium, High })
        {
            string fullPath = Path.Combine(
                mediaRoot,
                path.Replace('/', Path.DirectorySeparatorChar)
            );
            Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
            File.WriteAllText(fullPath, path);
        }

        return (Config(mediaRoot), [new(Low, 1, 1, 1), new(Medium, 1, 1, 1), new(High, 1, 1, 1)]);
    }

    private static object[] OperationValues(VideoVariantLooseCleanupManifest manifest) =>
        manifest
            .Operations.Select(operation =>
                (object)
                    new
                    {
                        Remove = manifest.Paths[operation.RemovePathIndex],
                        Keep = manifest.Paths[operation.KeepPathIndex],
                        operation.PartitionId,
                        operation.OriginalSize,
                        operation.OriginalLastWriteUtcTicks,
                        operation.DirectPresent,
                        operation.DirectSize,
                        operation.DirectLastWriteUtcTicks,
                    }
            )
            .ToArray();

    private static Dictionary<string, VideoVariantLooseCacheRow> Rows(
        params (string Path, int Partition)[] rows
    ) =>
        rows.ToDictionary(
            row => row.Path,
            row => new VideoVariantLooseCacheRow(row.Path, 1, 1, row.Partition),
            StringComparer.Ordinal
        );

    private static VideoVariantLooseCacheRow Row(string path) => new(path, 1, 1, 1);

    private static void WriteMedia(string root, string path, long size)
    {
        string fullPath = Path.Combine(root, path.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        using FileStream stream = File.Create(fullPath);
        stream.SetLength(size);
    }

    private static VideoVariantLooseCleanupManifest SourceManifest(
        string directRoot,
        params (string Remove, string Keep, int Partition, long Size)[] values
    )
    {
        string[] paths = values
            .SelectMany(value => new[] { value.Remove, value.Keep })
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToArray();
        Dictionary<string, int> indices = paths
            .Select((path, index) => (path, index))
            .ToDictionary(value => value.path, value => value.index, StringComparer.Ordinal);
        VideoVariantLooseCleanupManifest manifest = new()
        {
            Version = 1,
            CreatedUtc = new DateTimeOffset(2026, 1, 2, 3, 4, 5, TimeSpan.Zero),
            DataConfigPath = "C:\\Data.json",
            MediaStoreId = "store",
            CacheId = "cache",
            CacheNamespace = "namespace",
            DirectRoot = directRoot,
            FileParallelism = 16,
            Paths = [.. paths],
            Operations = values
                .Select(value => new VideoVariantLooseCleanupOperation
                {
                    RemovePathIndex = indices[value.Remove],
                    KeepPathIndex = indices[value.Keep],
                    PartitionId = value.Partition,
                    OriginalSize = value.Size,
                })
                .ToList(),
        };
        manifest.ApprovalHash = VideoVariantLooseCleanupPreviewService.CalculateApprovalHash(
            manifest
        );
        return manifest;
    }

    private sealed class InlineProgress<T>(Action<T> report) : IProgress<T>
    {
        public void Report(T value) => report(value);
    }

    private ResolvedConfig Config(string mediaRoot, string? configPath = null)
    {
        configPath ??= Path.Combine(_root, "Data.json");
        if (!File.Exists(configPath))
        {
            Directory.CreateDirectory(_root);
            File.WriteAllText(configPath, "{}");
        }
        FileInfo info = new(configPath);
        return new(
            configPath,
            info.Length,
            info.LastWriteTimeUtc.Ticks,
            "store",
            "cache",
            "unused",
            new() { [1] = mediaRoot },
            []
        );
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
            Directory.Delete(_root, recursive: true);
    }
}
