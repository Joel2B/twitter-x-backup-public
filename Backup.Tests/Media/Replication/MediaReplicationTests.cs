using Backup.Application.Media;
using Backup.Application.Media.Models;
using Backup.Infrastructure.Media.Abstractions.Services;
using Backup.Infrastructure.Media.Models;
using Backup.Infrastructure.Media.Services;
using Microsoft.Extensions.Logging.Abstractions;

namespace Backup.Tests;

public class MediaReplicationTests
{
    [Fact]
    public async Task Replicate_WhenReadIsCancelled_ThrowsOperationCanceledException()
    {
        MediaReplication sut = new(
            new NullLogger<MediaReplication>(),
            new FakePlanningService(),
            new FakeDownloadModelMapper()
        );

        List<Download> downloads =
        [
            new Download
            {
                Id = "d1",
                Data = [new DataDownload { Url = "https://x/media.jpg", Path = "media/1.jpg" }],
            },
        ];

        IMediaStorage source = new ThrowOnReadStorage { Id = "source" };
        IMediaStorage target = new MemoryStorage { Id = "target" };

        await Assert.ThrowsAsync<OperationCanceledException>(
            () => sut.Replicate(downloads, [source], target, CancellationToken.None)
        );
    }

    private sealed class FakePlanningService : IMediaReplicationPlanningService
    {
        public IReadOnlyList<MediaReplicationCopyAction> SelectCopyActions(
            IEnumerable<MediaReplicationPathObservation> observations
        ) =>
            observations
                .Select(observation => new MediaReplicationCopyAction
                {
                    DownloadId = observation.DownloadId,
                    Url = observation.Url,
                    Path = observation.Path,
                })
                .ToList();

        public IReadOnlyList<MediaDownload> RemoveCopied(
            IEnumerable<MediaDownload> downloads,
            IEnumerable<MediaReplicationCopyAction> copied
        ) => downloads.ToList();
    }

    private sealed class FakeDownloadModelMapper : IMediaDownloadModelMapper
    {
        public List<MediaDownload> ToApplication(IEnumerable<Download> downloads) =>
            downloads
                .Select(download => new MediaDownload
                {
                    Id = download.Id,
                    Data = download
                        .Data.Select(data => new MediaDownloadData
                        {
                            Url = data.Url,
                            Path = data.Path,
                        })
                        .ToList(),
                })
                .ToList();

        public List<Download> ToInfrastructure(IEnumerable<MediaDownload> downloads) =>
            downloads
                .Select(download => new Download
                {
                    Id = download.Id,
                    Data = download
                        .Data.Select(data => new DataDownload { Url = data.Url, Path = data.Path })
                        .ToList(),
                })
                .ToList();
    }

    private sealed class ThrowOnReadStorage : IMediaStorage
    {
        public string? Id { get; set; }

        public Task Save(Stream stream, string path, CancellationToken token) => Task.CompletedTask;

        public Task<bool> Exists(string path) => Task.FromResult(true);

        public Task<Stream> Read(string path) => throw new OperationCanceledException();

        public Task<Stream> Write(string path) => Task.FromResult<Stream>(new MemoryStream());

        public Task<string?> GetHash(string path) => Task.FromResult<string?>(null);

        public Task<MediaCacheEntry?> GetCache(string path) =>
            Task.FromResult<MediaCacheEntry?>(null);

        public Stream GetTempStream() => new MemoryStream();
    }

    private sealed class MemoryStorage : IMediaStorage
    {
        public string? Id { get; set; }

        public Task Save(Stream stream, string path, CancellationToken token) => Task.CompletedTask;

        public Task<bool> Exists(string path) => Task.FromResult(false);

        public Task<Stream> Read(string path) => Task.FromResult<Stream>(new MemoryStream());

        public Task<Stream> Write(string path) => Task.FromResult<Stream>(new MemoryStream());

        public Task<string?> GetHash(string path) => Task.FromResult<string?>(null);

        public Task<MediaCacheEntry?> GetCache(string path) =>
            Task.FromResult<MediaCacheEntry?>(null);

        public Stream GetTempStream() => new MemoryStream();
    }
}
