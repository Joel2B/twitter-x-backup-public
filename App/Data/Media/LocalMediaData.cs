using Backup.App.Interfaces;
using Backup.App.Interfaces.Partition;
using Backup.App.Interfaces.Services.Media;
using Backup.App.Models.Media;
using Backup.App.Models.Utils;
using Microsoft.Extensions.Logging;

namespace Backup.App.Data.Media;

public class LocalMediaData(
    ILogger<LocalMediaData> _log,
    Models.Config.Data.Media.Storage _config,
    IPartition _partition,
    LocalMediaCache _mediaCache
) : IMediaData, ISetup
{
    public string? Id { get; set; }

    private readonly ILogger<LocalMediaData> _logger = _log;
    private readonly Models.Config.Data.Media.Storage _config = _config;
    private readonly IPartition _partition = _partition;
    private readonly LocalMediaCache _mediaCache = _mediaCache;

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
        Models.Config.Data.Partition heavy = _partition.GetHeavy();

        return Path.Combine(
            [.. heavy.Paths, .. _config.Paths.Tmp.Paths, .. _config.Paths.Tmp.Downloader.Paths]
        );
    }

    public async Task CheckData(List<Download> downloads)
    {
        DeleteTemp();
        await _mediaCache.Load();

        foreach (Download download in downloads)
        {
            download.Data.RemoveAll(data =>
            {
                Cache? cache = _mediaCache.Get(data.Path);

                return cache is not null && cache?.Size?.File is long sz && sz != 0;
            });
        }

        downloads.RemoveAll(dl => dl.Data.Count == 0);
    }

    public async Task CheckIntegrity(List<Download> downloads)
    {
        int nullCount = 0;
        int sizeCount = 0;
        int invalidCount = 0;

        foreach (Download download in downloads)
        {
            for (int i = download.Data.Count - 1; i >= 0; i--)
            {
                DataDownload data = download.Data[i];

                long? size = _mediaCache.Get(data.Path)?.Size?.File;
                string fullPath = "";

                if (size is not null)
                    fullPath = await _mediaCache.GetPath(data.Path);

                bool remove = false;

                if (size is null)
                {
                    remove = true;
                    nullCount++;
                }

                if (size >= 1000)
                    remove = true;
                else
                    sizeCount++;

                if (!remove)
                    if (
                        MediaValidator.IsValid(
                            fullPath,
                            () => _logger.LogWarning("path {path} not exist", fullPath)
                        )
                    )
                        remove = true;
                    else
                        invalidCount++;

                if (remove)
                    download.Data.RemoveAt(i);
            }
        }

        downloads.RemoveAll(dl => dl.Data.Count == 0);

        _logger.LogInformation(
            "null: {nullCount}, size: {sizeCount}, invalid: {invalidCount}",
            nullCount,
            sizeCount,
            invalidCount
        );
    }

    public async Task Prune(List<Download> downloads)
    {
        if (!_config.Tasks.Prune)
            return;

        await _mediaCache.Load();

        HashSet<string> paths = downloads
            .SelectMany(download => download.Data.Select(o => o.Path))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        CancellationTokenSource cts = new();

        ParallelOptions options = new()
        {
            MaxDegreeOfParallelism = 64,
            CancellationToken = cts.Token,
        };

        await Parallel.ForEachAsync(
            paths,
            options,
            async (path, ct) =>
            {
                try
                {
                    string fullPath = await _mediaCache.GetPath(path, ct: ct);

                    if (!File.Exists(fullPath))
                        return;

                    File.Delete(fullPath);
                    _logger.LogInformation("media deleted: {path}", fullPath);
                }
                catch (Exception ex)
                {
                    _logger.LogError("error deleting {media}: {error}", path, ex.Message);
                }
            }
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
        await Utils.FileHasher.GetFileHash(await _mediaCache.GetPath(path));

    public Task<Cache?> GetCache(string path) => Task.FromResult(_mediaCache.Get(path));

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

    private void DeleteTemp()
    {
        string path = GetPathTemp();

        if (!Directory.Exists(path))
            return;

        Directory.Delete(path, recursive: true);
        Directory.CreateDirectory(path);
    }
}
