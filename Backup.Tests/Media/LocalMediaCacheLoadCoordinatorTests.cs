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
        public int SavePrimarySnapshotCalls { get; private set; }
        public int ApplyRecheckChangesCalls { get; private set; }

        public Task<bool> PrimarySnapshotExists(
            string file,
            CancellationToken cancellationToken = default
        ) => Task.FromResult(true);

        public Task<IReadOnlyList<MediaCacheEntry>> LoadIncrementalSnapshots(
            string directory,
            CancellationToken cancellationToken = default
        ) => Task.FromResult<IReadOnlyList<MediaCacheEntry>>([]);

        public Task<IReadOnlyList<MediaCacheEntry>> LoadPrimarySnapshot(
            string file,
            CancellationToken cancellationToken = default
        ) => Task.FromResult<IReadOnlyList<MediaCacheEntry>>([]);

        public Task SavePrimarySnapshot(
            string file,
            IReadOnlyCollection<MediaCacheEntry> entries,
            CancellationToken cancellationToken = default
        )
        {
            SavePrimarySnapshotCalls++;
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
