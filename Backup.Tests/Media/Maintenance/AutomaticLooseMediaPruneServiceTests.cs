using Backup.Infrastructure.Media.Maintenance;

namespace Backup.Tests.Media.Maintenance;

public sealed class AutomaticLooseMediaPruneServiceTests
{
    [Theory]
    [InlineData("1/photo/2/source/jpg/size/orig.jpg", "1/photo/2/source")]
    [InlineData("1/video/2/thumb/source/jpg/size/orig.jpg", "1/video/2/thumb/source")]
    [InlineData("1/gif/2/thumb/source/png/size/orig.png", "1/gif/2/thumb/source")]
    [InlineData("1/video/2/mp4/source/1920x1080.mp4", "1/video/2")]
    public void Group_keeps_media_boundaries(string path, string expected)
    {
        Assert.Equal(expected, AutomaticLooseMediaPruneService.Group(path));
    }

    [Fact]
    public void Normalize_accepts_cache_separator_variants()
    {
        Assert.Equal(
            "1/photo/2/source/jpg/size/orig.jpg",
            AutomaticLooseMediaPruneService.Normalize(
                "1\\photo\\2\\source\\jpg\\size\\orig.jpg"
            )
        );
    }

    [Fact]
    public void Approval_hash_is_deterministic_and_covers_operations()
    {
        Guid runId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        AutomaticLooseMediaPruneService.PruneOperation operation = new(
            runId,
            0,
            "1/photo/2/source/jpg/size/large.jpg",
            7,
            123,
            456,
            "1/photo/2/source/jpg/size/orig.jpg",
            7,
            789,
            101112,
            "pending"
        );

        string first = AutomaticLooseMediaPruneService.CalculateApprovalHash(
            runId,
            "nas media",
            "postgres",
            "namespace",
            ["incremental-1", "incremental-2"],
            new('c', 64),
            new('d', 64),
            new('a', 64),
            new('b', 64),
            [operation]
        );
        string second = AutomaticLooseMediaPruneService.CalculateApprovalHash(
            runId,
            "nas media",
            "postgres",
            "namespace",
            ["incremental-1", "incremental-2"],
            new('c', 64),
            new('d', 64),
            new('a', 64),
            new('b', 64),
            [operation with { FileSize = 124 }]
        );

        Assert.Equal(64, first.Length);
        Assert.NotEqual(first, second);
        Assert.True(AutomaticLooseMediaPruneService.FixedEquals(first, first));
        Assert.False(AutomaticLooseMediaPruneService.FixedEquals(first, second));
        Assert.False(AutomaticLooseMediaPruneService.FixedEquals(first, "invalid"));
    }

    [Fact]
    public void Approval_hash_covers_config_roots_and_exact_incremental_namespaces()
    {
        Guid runId = Guid.Parse("11111111-1111-1111-1111-111111111111");

        string first = AutomaticLooseMediaPruneService.CalculateApprovalHash(
            runId, "store", "cache", "primary", ["incremental-a", "incremental-b"],
            new('a', 64), new('b', 64), new('c', 64), new('d', 64), []
        );
        string reordered = AutomaticLooseMediaPruneService.CalculateApprovalHash(
            runId, "store", "cache", "primary", ["incremental-b", "incremental-a"],
            new('a', 64), new('b', 64), new('c', 64), new('d', 64), []
        );
        string changed = AutomaticLooseMediaPruneService.CalculateApprovalHash(
            runId, "store", "cache", "primary", ["incremental-a", "incremental-c"],
            new('a', 64), new('b', 64), new('c', 64), new('d', 64), []
        );

        Assert.Equal(first, reordered);
        Assert.NotEqual(first, changed);
    }

    [Fact]
    public void Roots_hash_is_order_independent_and_covers_resolved_paths()
    {
        Dictionary<int, string> first = new() { [2] = Path.Combine("root", "two"), [1] = Path.Combine("root", "one") };
        Dictionary<int, string> reordered = new() { [1] = Path.Combine("root", "one"), [2] = Path.Combine("root", "two") };
        Dictionary<int, string> changed = new() { [1] = Path.Combine("root", "one"), [2] = Path.Combine("root", "other") };

        Assert.Equal(
            AutomaticLooseMediaPruneService.CalculateRootsHash(first),
            AutomaticLooseMediaPruneService.CalculateRootsHash(reordered)
        );
        Assert.NotEqual(
            AutomaticLooseMediaPruneService.CalculateRootsHash(first),
            AutomaticLooseMediaPruneService.CalculateRootsHash(changed)
        );
    }

    [Fact]
    public void Quarantine_name_is_fixed_length_and_path_sensitive()
    {
        string first = AutomaticLooseMediaPruneService.CalculateQuarantineName(new string('a', 1000) + "/file.jpg");
        string second = AutomaticLooseMediaPruneService.CalculateQuarantineName(new string('a', 1000) + "/other.jpg");

        Assert.Equal(68, first.Length);
        Assert.EndsWith(".bin", first);
        Assert.NotEqual(first, second);
    }

    [Fact]
    public void Config_loader_resolves_env_aliases_partitions_and_ignores_irrelevant_sections()
    {
        string root = Path.Combine(Path.GetTempPath(), $"prune-config-{Guid.NewGuid():N}");
        string variable = $"PRUNE_TEST_{Guid.NewGuid():N}";
        string connectionString = "Host=localhost;Database=prune";
        Directory.CreateDirectory(root);
        Environment.SetEnvironmentVariable(variable, connectionString);
        try
        {
            string configPath = Path.Combine(root, "Data.json");
            File.WriteAllText(
                configPath,
                $$"""
                {
                  "Aliases": { "ROOT": {{System.Text.Json.JsonSerializer.Serialize(root)}} },
                  "Partitions": [
                    { "Id": 7, "Paths": ["@ROOT", "partition"] }
                  ],
                  "Post": [{}],
                  "Dump": [{ "Paths": {} }],
                  "Media": [{
                    "Id": "store",
                    "Cache": [{
                      "Id": "cache",
                      "Type": "POSTGRES",
                      "ConnectionString": "ENV:{{variable}}"
                    }],
                    "Paths": { "Media": { "Paths": ["content"] } },
                    "Partitions": [7, 7]
                  }]
                }
                """
            );

            MediaPruneConfig config = MediaPruneConfigLoader.Load(configPath, "store", "cache");

            Assert.Equal(connectionString, config.ConnectionString);
            Assert.Equal("store", config.MediaStoreId);
            Assert.Equal("cache", config.CacheId);
            KeyValuePair<int, string> resolved = Assert.Single(config.MediaRoots);
            Assert.Equal(7, resolved.Key);
            Assert.Equal(Path.GetFullPath(Path.Combine(root, "partition", "content")), resolved.Value);
        }
        finally
        {
            Environment.SetEnvironmentVariable(variable, null);
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void Config_loader_rejects_unresolved_store_partition()
    {
        string root = Path.Combine(Path.GetTempPath(), $"prune-config-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        try
        {
            string configPath = Path.Combine(root, "Data.json");
            File.WriteAllText(
                configPath,
                $$"""
                {
                  "Aliases": { "ROOT": {{System.Text.Json.JsonSerializer.Serialize(root)}} },
                  "Partitions": [{ "Id": 1, "Enabled": false, "Paths": ["@ROOT"] }],
                  "Media": [{
                    "Id": "store",
                    "Cache": [{
                      "Id": "cache",
                      "Type": "postgres",
                      "ConnectionString": "Host=localhost"
                    }],
                    "Paths": { "Media": { "Paths": ["content"] } },
                    "Partitions": [1]
                  }]
                }
                """
            );

            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
                () => MediaPruneConfigLoader.Load(configPath, "store", "cache")
            );
            Assert.Contains("Partition 1", exception.Message, StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void Config_loader_requires_exact_case_sensitive_ids()
    {
        string root = Path.Combine(Path.GetTempPath(), $"prune-config-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        try
        {
            string configPath = Path.Combine(root, "Data.json");
            File.WriteAllText(
                configPath,
                $$"""
                {
                  "Aliases": { "ROOT": {{System.Text.Json.JsonSerializer.Serialize(root)}} },
                  "Partitions": [{ "Id": 1, "Paths": ["@ROOT"] }],
                  "Media": [{
                    "Id": "Store",
                    "Cache": [{
                      "Id": "cache",
                      "Type": "postgres",
                      "ConnectionString": "Host=localhost"
                    }],
                    "Paths": { "Media": { "Paths": ["content"] } },
                    "Partitions": [1]
                  }]
                }
                """
            );

            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
                () => MediaPruneConfigLoader.Load(configPath, "store", "cache")
            );
            Assert.Contains("found 0", exception.Message, StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }
}
