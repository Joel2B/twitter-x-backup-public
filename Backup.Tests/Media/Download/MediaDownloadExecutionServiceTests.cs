using Backup.Application.Media;
using Backup.Application.Media.Models;
using Backup.Application.Media.Ports;

namespace Backup.Tests;

public class MediaDownloadExecutionServiceTests
{
    [Fact]
    public async Task Run_CompletesAndSavesStateAndLogs()
    {
        MediaDownloadExecutionService sut = new();
        FakeCommand command = new();
        FakeRunner runner = new();
        MediaDownloadQueueItem[] queue =
        [
            new()
            {
                DownloadId = "1",
                Url = "u1",
                Path = "p1",
            },
            new()
            {
                DownloadId = "2",
                Url = "u2",
                Path = "p2",
            },
        ];
        MediaParallelDownloadSettings settings = CreateSettings();

        await sut.Run(command, runner, queue, settings);

        Assert.Equal(2, command.SuccessCount);
        Assert.Equal(1, command.SaveStateCount);
        Assert.Equal(1, command.SaveLogsCount);
        Assert.Equal(0, command.ItemErrorCount);
        Assert.Equal(0, command.FatalErrorCount);
    }

    [Fact]
    public async Task Run_Cancels_WhenCommandMarksErrorAsCancelable()
    {
        MediaDownloadExecutionService sut = new();
        FakeCommand command = new() { ThrowOnFirstDownload = true, CancelOnError = true };
        FakeRunner runner = new();
        MediaDownloadQueueItem[] queue =
        [
            new()
            {
                DownloadId = "1",
                Url = "u1",
                Path = "p1",
            },
            new()
            {
                DownloadId = "2",
                Url = "u2",
                Path = "p2",
            },
        ];
        MediaParallelDownloadSettings settings = CreateSettings();

        await sut.Run(command, runner, queue, settings);

        Assert.Equal(0, command.SuccessCount);
        Assert.Equal(0, command.ItemErrorCount);
        Assert.Equal(1, command.SaveStateCount);
        Assert.Equal(1, command.SaveLogsCount);
    }

    private static MediaParallelDownloadSettings CreateSettings() =>
        new()
        {
            MinDegreeOfParallelism = 1,
            MaxDegreeOfParallelism = 2,
            StartDegreeOfParallelism = 1,
            TargetDuration = TimeSpan.FromSeconds(1),
            JumpToMaxOnFastAverage = false,
            EnableHeavyCut = false,
            HeavyThreshold = TimeSpan.FromSeconds(2),
            StrictDecreaseGate = false,
            EnableDebug = false,
        };

    private sealed class FakeRunner : IMediaDownloadParallelRunner
    {
        public async Task Run(
            IReadOnlyList<MediaDownloadQueueItem> queue,
            MediaParallelDownloadSettings settings,
            Func<MediaDownloadQueueItem, CancellationToken, Task> processItem,
            Action<string> debugSink,
            CancellationToken cancellationToken
        )
        {
            foreach (MediaDownloadQueueItem item in queue)
            {
                cancellationToken.ThrowIfCancellationRequested();
                await processItem(item, cancellationToken);
            }
        }
    }

    private sealed class FakeCommand : IMediaDownloadExecutionCommand
    {
        public bool ThrowOnFirstDownload { get; init; }

        public bool CancelOnError { get; init; }

        public int SuccessCount { get; private set; }

        public int ItemErrorCount { get; private set; }

        public int FatalErrorCount { get; private set; }

        public int SaveStateCount { get; private set; }

        public int SaveLogsCount { get; private set; }

        private int _downloadCount;

        public Task<Stream> Download(
            MediaDownloadQueueItem item,
            CancellationToken cancellationToken
        )
        {
            _downloadCount++;

            if (ThrowOnFirstDownload && _downloadCount == 1)
                throw new InvalidOperationException("test");

            return Task.FromResult<Stream>(new MemoryStream([1, 2, 3]));
        }

        public Task Save(
            MediaDownloadQueueItem item,
            Stream stream,
            CancellationToken cancellationToken
        ) => Task.CompletedTask;

        public void OnSuccess(MediaDownloadQueueItem item) => SuccessCount++;

        public void OnItemError(MediaDownloadQueueItem item, string message) => ItemErrorCount++;

        public bool ShouldCancelOnItemError(Exception exception) => CancelOnError;

        public void OnFatalError(string message) => FatalErrorCount++;

        public void OnDebug(string message) { }

        public Task SaveState()
        {
            SaveStateCount++;
            return Task.CompletedTask;
        }

        public Task SaveLogs()
        {
            SaveLogsCount++;
            return Task.CompletedTask;
        }
    }
}
