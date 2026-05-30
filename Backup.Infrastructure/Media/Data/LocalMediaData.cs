using Backup.Infrastructure.Interfaces;
using Backup.Infrastructure.Core.Abstractions.Partition;
using Backup.Infrastructure.Media.Abstractions.Services;
using Backup.Infrastructure.Models.Config.Data;
using Backup.Infrastructure.Models.Config.Data.Media;
using Backup.Infrastructure.Models.Media;

namespace Backup.Infrastructure.Data.Media;

public class LocalMediaData(
    StorageMedia _config,
    IPartition _partition,
    IMediaCache _mediaCache
) : IMediaStorage, ISetup
{
    public string? Id { get; set; }

    private readonly StorageMedia _config = _config;
    private readonly IPartition _partition = _partition;
    private readonly IMediaCache _mediaCache = _mediaCache;

    public Task Setup()
    {
        SetupDirectory();

        return Task.CompletedTask;
    }

    private void SetupDirectory()
    {
        Directory.CreateDirectory(GetPathTemp());
    }

    private string GetPathTemp()
    {
        PartitionConfig heavy = _partition.GetHeavy();

        return Path.Combine(
            [.. heavy.Paths, .. _config.Paths.Tmp.Paths, .. _config.Paths.Tmp.Downloader.Paths]
        );
    }

    public async Task Save(Stream stream, string path, CancellationToken token)
    {
        long size = stream.Length;
        stream.Position = 0;

        string? fullPath = await _mediaCache.GetPath(path, size);
        string? directoryName = Path.GetDirectoryName(fullPath);

        if (directoryName is null)
            throw new Exception("Error getting the directory name.");

        Directory.CreateDirectory(directoryName);

        using FileStream fs = File.Create(fullPath);
        await stream.CopyToAsync(fs, token);
    }

    public async Task<bool> Exists(string path) => File.Exists(await _mediaCache.GetPath(path));

    public async Task<Stream> Read(string path)
    {
        string fullPath = await _mediaCache.GetPath(path);

        Stream stream = new FileStream(
            fullPath,
            new FileStreamOptions
            {
                Mode = FileMode.Open,
                Access = FileAccess.Read,
                Share = FileShare.Read,
                BufferSize = 128 * 1024,
                Options = FileOptions.Asynchronous | FileOptions.SequentialScan,
            }
        );

        return stream;
    }

    public async Task<Stream> Write(string path)
    {
        string fullPath = await _mediaCache.GetPath(path);
        string? directory = Path.GetDirectoryName(fullPath);

        if (directory is null)
            throw new Exception("Error getting the directory name.");

        Directory.CreateDirectory(directory);
        Stream stream = File.Open(fullPath, FileMode.Create, FileAccess.Write, FileShare.None);

        return stream;
    }

    public async Task<string?> GetHash(string path) =>
        await Backup.Infrastructure.Utils.FileHasher.GetFileHash(await _mediaCache.GetPath(path));

    public Task<MediaCacheEntry?> GetCache(string path) => Task.FromResult(_mediaCache.Get(path));

    public Stream GetTempStream()
    {
        string path = Path.Combine([GetPathTemp(), Guid.NewGuid().ToString("N")]);

        Stream stream = new FileStream(
            path,
            FileMode.Create,
            FileAccess.ReadWrite,
            FileShare.None,
            128 * 1024,
            useAsync: true
        );

        return stream;
    }
}
