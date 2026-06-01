using Backup.Application.Media;
using Backup.Application.Media.Models;
using Backup.Application.Media.Ports;
using Backup.Domain.Posts;

namespace Backup.Tests;

public class MediaOrchestrationServiceTests
{
    [Fact]
    public async Task Run_NoPosts_DoesNothing()
    {
        MediaOrchestrationService sut = new();
        FakeMediaCommand command = new() { MediaInputs = [] };

        await sut.Run(command);

        Assert.Equal(0, command.ProcessCalls);
        Assert.Equal(0, command.BackupCalls);
    }

    [Fact]
    public async Task Run_WithPosts_ExecutesPipelineAndBackups()
    {
        MediaOrchestrationService sut = new();
        FakeMediaCommand command = new()
        {
            MediaInputs = [CreateMediaInput("p1")],
            ProcessingResult = new MediaProcessingResult
            {
                All = [CreateDownload("p1", "https://a/1.jpg", "m/1.jpg")],
                Filtered = [CreateDownload("p1", "https://a/1.jpg", "m/1.jpg")],
            },
            StorageIds = ["local"],
            MaintenanceByStorage = new Dictionary<string, bool> { ["local"] = true },
        };

        await sut.Run(command);

        Assert.Equal(1, command.ProcessCalls);
        Assert.Equal(1, command.PruneCalls);
        Assert.True(command.FilterCalls >= 2);
        Assert.Equal(1, command.DownloadCalls);
        Assert.Equal(1, command.ReplicationCalls);
        Assert.Equal(1, command.BackupCalls);
    }

    private static MediaInput CreateMediaInput(string id) =>
        new()
        {
            Id = id,
            Profile = new PostProfile { Id = "u1", UserName = "user1" },
            Medias =
            [
                new PostMedia
                {
                    Id = "m1",
                    Url = "https://a/1.jpg",
                    Type = "photo",
                },
            ],
            Deleted = false,
        };

    private static MediaDownload CreateDownload(string id, string url, string path) =>
        new() { Id = id, Data = [new MediaDownloadData { Url = url, Path = path }] };

    private sealed class FakeMediaCommand : IMediaOrchestrationCommand
    {
        public List<MediaInput> MediaInputs { get; set; } = [];
        public MediaProcessingResult ProcessingResult { get; set; } =
            new() { All = [], Filtered = [] };
        public List<string> StorageIds { get; set; } = [];
        public Dictionary<string, bool> MaintenanceByStorage { get; set; } = [];

        public int ProcessCalls { get; private set; }
        public int PruneCalls { get; private set; }
        public int FilterCalls { get; private set; }
        public int DownloadCalls { get; private set; }
        public int ReplicationCalls { get; private set; }
        public int BackupCalls { get; private set; }

        public Task<IReadOnlyList<MediaInput>> GetMediaInputs(
            CancellationToken cancellationToken = default
        ) => Task.FromResult<IReadOnlyList<MediaInput>>(MediaInputs);

        public Task<MediaProcessingResult> Process(
            IReadOnlyList<MediaInput> posts,
            CancellationToken cancellationToken = default
        )
        {
            ProcessCalls++;
            return Task.FromResult(ProcessingResult);
        }

        public Task Prune(
            List<MediaDownload> downloads,
            CancellationToken cancellationToken = default
        )
        {
            PruneCalls++;
            return Task.CompletedTask;
        }

        public Task Filter(
            List<MediaDownload> downloads,
            CancellationToken cancellationToken = default
        )
        {
            FilterCalls++;
            return Task.CompletedTask;
        }

        public IReadOnlyList<string> GetStorageIds() => StorageIds;

        public bool HasMaintenance(string storageId) =>
            MaintenanceByStorage.TryGetValue(storageId, out bool enabled) && enabled;

        public Task PruneStorage(
            string storageId,
            List<MediaDownload> downloads,
            CancellationToken cancellationToken = default
        ) => Task.CompletedTask;

        public Task CheckStorageData(
            string storageId,
            List<MediaDownload> downloads,
            CancellationToken cancellationToken = default
        ) => Task.CompletedTask;

        public Task CheckStorageIntegrity(
            string storageId,
            List<MediaDownload> downloads,
            CancellationToken cancellationToken = default
        ) => Task.CompletedTask;

        public Task DownloadToStorage(
            string storageId,
            List<MediaDownload> downloads,
            CancellationToken cancellationToken = default
        )
        {
            DownloadCalls++;
            return Task.CompletedTask;
        }

        public Task ReplicateFromStorage(
            string storageId,
            List<MediaDownload> downloads,
            CancellationToken cancellationToken = default
        )
        {
            ReplicationCalls++;
            return Task.CompletedTask;
        }

        public Task RunBackups(
            List<MediaDownload> downloads,
            CancellationToken cancellationToken = default
        )
        {
            BackupCalls++;
            return Task.CompletedTask;
        }
    }
}
