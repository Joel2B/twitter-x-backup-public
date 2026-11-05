using System.Collections.Concurrent;
using Backup.Application.IO;
using Backup.Application.Media.Maintenance;
using Backup.Application.Media.Maintenance.Models;
using Backup.Infrastructure.Core.Abstractions.Partition;
using Backup.Infrastructure.DependencyInjection.Features.Media;
using Backup.Infrastructure.Media.Abstractions.Services;
using Backup.Infrastructure.Media.Data;
using Backup.Infrastructure.Media.Models;
using Backup.Infrastructure.Models.Config.Data;
using Backup.Infrastructure.Models.Config.Data.Media;
using Backup.Infrastructure.Models.Config.Downloads;
using Microsoft.Extensions.Logging.Abstractions;

namespace Backup.Tests;

public sealed class LocalMediaCacheLoadCoordinatorTests
{
    [Fact]
    public async Task Load_WithZeroMutations_DoesNotPersistSnapshotOrRecheckChanges()
    {
        PartitionConfig partitionConfig = new()
        {
            Id = 1,
            Type = "local",
            Size = 0,
            UsableSpace = 0,
            Paths = [Path.GetTempPath()],
        };
        FakePersistence persistence = new();
        MediaCacheTargetRuntime target = new(
            "local",
            "local",
            true,
            MediaDataInfrastructureServiceCollectionExtensions.MediaCacheType.Json,
            new PathConfig { Paths = ["cache"], File = "media.json" },
            [partitionConfig],
            partitionConfig,
            [],
            persistence
        );
        LocalMediaCachePathLayout pathLayout = new(
            new StorageMedia
            {
                Type = "local",
                Enabled = true,
                Partitions = [partitionConfig.Id],
                Tasks = new Tasks { Prune = false },
                Paths = new Paths
                {
                    Paths = [],
                    Media = new PathConfig { Paths = ["media"] },
                    Tmp = new Tmp
                    {
                        Paths = ["tmp"],
                        Downloader = new PathConfig { Paths = [] },
                        Downloaded = new PathConfig { Paths = ["downloaded"] },
                    },
                },
            },
            null!,
            [target],
            new FakeDataStoreGuardService(),
            null!
        );
        LocalMediaCacheSnapshotCoordinator snapshotCoordinator = new(
            [target],
            new Backup.Application.Core.PrimarySelectionService(),
            new MediaCacheEntryPathPolicyService(),
            new MediaCacheReplicationPathService(),
            new MediaCacheJsonSnapshotService(),
            pathLayout,
            NullLogger<LocalMediaCacheSnapshotCoordinator>.Instance
        );
        LocalMediaCacheMutationApplier mutationApplier = new(
            NullLogger.Instance,
            new EmptyMutationExecutionService()
        );
        LocalMediaCacheLoadCoordinator sut = new(
            NullLogger.Instance,
            new FakePartition(partitionConfig),
            new EmptyLoadExecutionService(),
            null!,
            new MediaCachePartitionSizeAggregationService(),
            new MediaCacheEntryPathPolicyService(),
            pathLayout,
            snapshotCoordinator,
            mutationApplier
        );

        await sut.Load(new ConcurrentDictionary<string, MediaCacheEntry>());

        Assert.Equal(0, persistence.SavePrimarySnapshotCalls);
        Assert.Equal(0, persistence.ApplyRecheckChangesCalls);
    }

    [Fact]
    public async Task SeedMissingTargets_CopiesLoadedCacheOnlyToEmptySecondary()
    {
        PartitionConfig partitionConfig = new()
        {
            Id = 1,
            Type = "local",
            Size = 0,
            UsableSpace = 0,
            Paths = [Path.GetTempPath()],
        };
        FakePersistence primaryPersistence = new();
        FakePersistence emptySecondaryPersistence = new() { SnapshotExists = false };
        FakePersistence populatedSecondaryPersistence = new();
        MediaCacheTargetRuntime primary = CreateTarget(
            "primary",
            true,
            partitionConfig,
            primaryPersistence
        );
        MediaCacheTargetRuntime emptySecondary = CreateTarget(
            "empty",
            false,
            partitionConfig,
            emptySecondaryPersistence
        );
        MediaCacheTargetRuntime populatedSecondary = CreateTarget(
            "populated",
            false,
            partitionConfig,
            populatedSecondaryPersistence
        );
        LocalMediaCachePathLayout pathLayout = CreatePathLayout(
            partitionConfig,
            [primary, emptySecondary, populatedSecondary]
        );
        LocalMediaCacheSnapshotCoordinator sut = new(
            [primary, emptySecondary, populatedSecondary],
            new Backup.Application.Core.PrimarySelectionService(),
            new MediaCacheEntryPathPolicyService(),
            new MediaCacheReplicationPathService(),
            new MediaCacheJsonSnapshotService(),
            pathLayout,
            NullLogger<LocalMediaCacheSnapshotCoordinator>.Instance
        );
        MediaCacheEntry entry = new() { Path = "media/item.jpg", PartitionId = 1 };

        await sut.SeedMissingTargets(
            new Dictionary<string, MediaCacheEntry>(StringComparer.OrdinalIgnoreCase)
            {
                [entry.Path] = entry,
            }
        );

        Assert.Equal(0, primaryPersistence.SavePrimarySnapshotCalls);
        Assert.Equal(1, emptySecondaryPersistence.SavePrimarySnapshotCalls);
        Assert.Equal([entry], emptySecondaryPersistence.SavedEntries);
        Assert.Equal(0, populatedSecondaryPersistence.SavePrimarySnapshotCalls);
    }

    [Fact]
    public async Task Incremental_snapshot_overrides_primary_partition_for_same_path()
    {
        PartitionConfig partition = new()
        {
            Id = 1,
            Type = "primary",
            Size = 0,
            UsableSpace = 0,
            Paths = [Path.GetTempPath()],
        };
        MediaCacheEntry primaryEntry = new() { Path = "item.jpg", PartitionId = 1 };
        MediaCacheEntry incrementalEntry = new() { Path = "item.jpg", PartitionId = 4 };
        FakePersistence persistence = new()
        {
            PrimaryEntries = [primaryEntry],
            IncrementalEntries = [incrementalEntry],
        };
        MediaCacheTargetRuntime target = CreateTarget("primary", true, partition, persistence);
        LocalMediaCachePathLayout pathLayout = CreatePathLayout(partition, [target]);
        LocalMediaCacheSnapshotCoordinator sut = new(
            [target],
            new Backup.Application.Core.PrimarySelectionService(),
            new MediaCacheEntryPathPolicyService(),
            new MediaCacheReplicationPathService(),
            new MediaCacheJsonSnapshotService(),
            pathLayout,
            NullLogger<LocalMediaCacheSnapshotCoordinator>.Instance
        );
        Dictionary<string, MediaCacheEntry> cache = new(StringComparer.OrdinalIgnoreCase);

        await sut.LoadPrimarySnapshotInto(cache);
        await sut.LoadIncrementalSnapshotsInto(cache);

        Assert.Equal(4, Assert.Single(cache).Value.PartitionId);
    }

    private static MediaCacheTargetRuntime CreateTarget(
        string key,
        bool isDefault,
        PartitionConfig partition,
        IMediaCachePersistenceIOService persistence
    ) =>
        new(
            key,
            key,
            isDefault,
            MediaDataInfrastructureServiceCollectionExtensions.MediaCacheType.Json,
            new PathConfig { Paths = ["cache", key], File = "media.json" },
            [partition],
            partition,
            [],
            persistence
        );

    private static LocalMediaCachePathLayout CreatePathLayout(
        PartitionConfig partitionConfig,
        IReadOnlyList<MediaCacheTargetRuntime> targets
    ) =>
        new(
            new StorageMedia
            {
                Type = "local",
                Enabled = true,
                Partitions = [partitionConfig.Id],
                Tasks = new Tasks { Prune = false },
                Paths = new Paths
                {
                    Paths = [],
                    Media = new PathConfig { Paths = ["media"] },
                    Tmp = new Tmp
                    {
                        Paths = ["tmp"],
                        Downloader = new PathConfig { Paths = [] },
                        Downloaded = new PathConfig { Paths = ["downloaded"] },
                    },
                },
            },
            null!,
            targets,
            new FakeDataStoreGuardService(),
            null!
        );

    private sealed class EmptyLoadExecutionService : IMediaCacheLoadExecutionService
    {
        public MediaCacheLoadExecutionResult Execute(
            IReadOnlyList<MediaCacheStoredEntry> entries,
            IReadOnlyCollection<string> existingCachePaths,
            Func<
                IReadOnlyList<MediaCacheRecheckProbeInput>,
                MediaCacheRecheckProbeExecutionResult
            > probe
        ) => new();
    }

    private sealed class EmptyMutationExecutionService : IMediaCacheRecheckMutationExecutionService
    {
        public MediaCacheRecheckMutationApplySelection Execute(
            IReadOnlyList<MediaCacheRecheckMutation> mutations,
            IReadOnlySet<string> existingPaths
        ) => new();
    }

    private sealed class FakePersistence : IMediaCachePersistenceIOService
    {
        public bool SnapshotExists { get; init; } = true;
        public int SavePrimarySnapshotCalls { get; private set; }
        public int ApplyRecheckChangesCalls { get; private set; }
        public IReadOnlyCollection<MediaCacheEntry> SavedEntries { get; private set; } = [];
        public IReadOnlyList<MediaCacheEntry> PrimaryEntries { get; init; } = [];
        public IReadOnlyList<MediaCacheEntry> IncrementalEntries { get; init; } = [];

        public Task<bool> PrimarySnapshotExists(
            string file,
            CancellationToken cancellationToken = default
        ) => Task.FromResult(SnapshotExists);

        public Task<IReadOnlyList<MediaCacheEntry>> LoadIncrementalSnapshots(
            string directory,
            CancellationToken cancellationToken = default
        ) => Task.FromResult(IncrementalEntries);

        public Task<IReadOnlyList<MediaCacheEntry>> LoadPrimarySnapshot(
            string file,
            CancellationToken cancellationToken = default
        ) => Task.FromResult(PrimaryEntries);

        public Task SavePrimarySnapshot(
            string file,
            IReadOnlyCollection<MediaCacheEntry> entries,
            CancellationToken cancellationToken = default
        )
        {
            SavePrimarySnapshotCalls++;
            SavedEntries = entries;
            return Task.CompletedTask;
        }

        public Task SaveIncrementalSnapshot(
            string directory,
            MediaCacheEntry entry,
            string fileName,
            CancellationToken cancellationToken = default
        ) => Task.CompletedTask;

        public Task ApplyRecheckChanges(
            string primaryFilePath,
            string incrementalDirectory,
            IReadOnlyCollection<MediaCacheEntry> finalEntries,
            IReadOnlyCollection<MediaCacheEntry> upserts,
            IReadOnlyCollection<string> removals,
            CancellationToken cancellationToken = default
        )
        {
            ApplyRecheckChangesCalls++;
            return Task.CompletedTask;
        }

        public Task ReplicatePrimarySnapshot(
            string primaryFilePath,
            IReadOnlyCollection<string> replicaPaths,
            CancellationToken cancellationToken = default
        ) => Task.CompletedTask;

        public void ResetIncrementalSnapshotDirectory(string directory) { }
    }

    private sealed class FakePartition(PartitionConfig partition) : IPartition
    {
        public List<PartitionConfig> GetPartitions(List<int>? ids = null) => [partition];

        public PartitionConfig GetPath(int? id = null, long size = 0) => partition;

        public List<PartitionConfig> GetCache() => [partition];

        public PartitionConfig GetPrimary() => partition;

        public PartitionConfig GetHeavy() => partition;

        public void SetupSizes(Dictionary<int, long> sizes) { }
    }

    private sealed class FakeDataStoreGuardService : IDataStoreGuardService
    {
        public string RequireConfiguredFileName(string? fileName) => fileName!;

        public void EnsureFileExists(string path) { }

        public T RequireDeserialized<T>(T? value, string message) => value!;

        public T RequireInitialized<T>(T? value, string message) => value!;

        public string RequireDirectoryName(string? directory, string message) => directory!;
    }
}
