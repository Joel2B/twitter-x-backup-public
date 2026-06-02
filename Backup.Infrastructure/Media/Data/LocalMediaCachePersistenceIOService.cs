using Backup.Application.Media.Maintenance;
using Backup.Infrastructure.Media.Abstractions.Services;
using Backup.Infrastructure.Media.Models;
using Newtonsoft.Json;

namespace Backup.Infrastructure.Media.Data;

public sealed class LocalMediaCachePersistenceIOService(
    IMediaCacheJsonSnapshotService mediaCacheJsonSnapshotService
) : IMediaCachePersistenceIOService
{
    private readonly IMediaCacheJsonSnapshotService _mediaCacheJsonSnapshotService =
        mediaCacheJsonSnapshotService;

    public async Task<IReadOnlyList<MediaCacheEntry>> LoadIncrementalSnapshots(
        string directory,
        CancellationToken cancellationToken = default
    )
    {
        if (!Directory.Exists(directory))
            return [];

        List<MediaCacheEntry> entries = [];

        foreach (
            string path in Directory.EnumerateFiles(
                directory,
                "*.cache",
                SearchOption.TopDirectoryOnly
            )
        )
        {
            cancellationToken.ThrowIfCancellationRequested();

            string json = await File.ReadAllTextAsync(path, cancellationToken);
            MediaCacheEntry? cache = JsonConvert.DeserializeObject<MediaCacheEntry>(json);

            if (cache is not null)
                entries.Add(cache);
        }

        return entries;
    }

    public async Task<IReadOnlyList<MediaCacheEntry>> LoadPrimarySnapshot(
        string file,
        CancellationToken cancellationToken = default
    )
    {
        if (!File.Exists(file))
            return [];

        List<MediaCacheEntry> entries = [];

        await foreach (
            MediaCacheEntry entry in LocalMediaCacheReader.Get(file, _mediaCacheJsonSnapshotService)
        )
        {
            cancellationToken.ThrowIfCancellationRequested();
            entries.Add(entry);
        }

        return entries;
    }

    public async Task SavePrimarySnapshot(
        string file,
        IReadOnlyCollection<MediaCacheEntry> entries,
        CancellationToken cancellationToken = default
    )
    {
        cancellationToken.ThrowIfCancellationRequested();
        await LocalMediaCacheReader.Save(
            file,
            [.. entries],
            _mediaCacheJsonSnapshotService
        );
    }

    public async Task SaveIncrementalSnapshot(
        string directory,
        MediaCacheEntry entry,
        string fileName,
        CancellationToken cancellationToken = default
    )
    {
        cancellationToken.ThrowIfCancellationRequested();
        string path = Path.Combine(directory, fileName);
        string json = JsonConvert.SerializeObject(entry);

        await using FileStream fs = new(
            path,
            FileMode.Create,
            FileAccess.Write,
            FileShare.None,
            4096,
            FileOptions.Asynchronous | FileOptions.WriteThrough
        );

        await using StreamWriter sw = new(fs);
        await sw.WriteAsync(json.AsMemory(), cancellationToken);
        await sw.FlushAsync(cancellationToken);
        fs.Flush(true);
    }

    public Task ReplicatePrimarySnapshot(
        string primaryFilePath,
        IReadOnlyCollection<string> replicaPaths,
        CancellationToken cancellationToken = default
    )
    {
        if (!File.Exists(primaryFilePath))
            return Task.CompletedTask;

        foreach (string path in replicaPaths)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (File.Exists(path))
                File.Delete(path);

            File.Copy(primaryFilePath, path);
        }

        return Task.CompletedTask;
    }

    public void ResetIncrementalSnapshotDirectory(string directory)
    {
        if (!Directory.Exists(directory))
            return;

        Directory.Delete(directory, recursive: true);
        Directory.CreateDirectory(directory);
    }
}
