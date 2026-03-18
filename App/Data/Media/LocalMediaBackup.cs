using Backup.App.Interfaces;
using Backup.App.Interfaces.Data.Media;
using Backup.App.Interfaces.Partition;
using Backup.App.Models.Media.Backup;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace Backup.App.Data.Media;

public class LocalMediaBackup(
    ILogger<LocalMediaBackup> _logger,
    Models.Config.Data.Backup.Storage _config,
    IPartition _partition
) : IMediaBackup, ISetup
{
    private readonly ILogger<LocalMediaBackup> _logger = _logger;
    private readonly Models.Config.Data.Backup.Storage _config = _config;
    private readonly IPartition _partition = _partition;

    public Task Setup()
    {
        SetupDirectory();

        return Task.CompletedTask;
    }

    public void SetupDirectory()
    {
        Directory.CreateDirectory(GetPath());
        Directory.CreateDirectory(GetPathChunks());
        Directory.CreateDirectory(GetPathDirect());
    }

    private string GetPath()
    {
        Models.Config.Data.Partition? backup = _partition
            .GetPartitions()
            .FirstOrDefault(o => o.Type == "backup");

        if (backup is null)
            throw new Exception();

        return System.IO.Path.Combine([.. backup.Paths, .. _config.Paths.Paths]);
    }

    private string GetPathChunks() => System.IO.Path.Combine([GetPath(), .. _config.Chunk.Paths]);

    private string GetPathDirect() => System.IO.Path.Combine([GetPath(), .. _config.Direct.Paths]);

    public async Task<Models.Media.Backup.Backup?> GetBackup()
    {
        if (_config.Chunk.File is null)
            return null;

        string path = System.IO.Path.Combine(GetPathChunks(), _config.Chunk.File);

        if (!File.Exists(path))
            return null;

        string content = await File.ReadAllTextAsync(path);

        Models.Media.Backup.Backup? backup =
            JsonConvert.DeserializeObject<Models.Media.Backup.Backup>(content);

        if (backup is null)
            return null;

        return backup;
    }

    public async Task<List<Chunk>?> GetChunks(CancellationToken token = default)
    {
        if (_config.Chunk.Data.File is null)
            return null;

        Models.Media.Backup.Backup? backup = await GetBackup();

        if (backup is null)
            return null;

        if (backup.Chunks.Ids.Count == 0)
            return [];

        Chunk[] chunks = new Chunk[backup.Chunks.Ids.Count];

        ParallelOptions options = new() { MaxDegreeOfParallelism = 16, CancellationToken = token };

        try
        {
            await Parallel.ForEachAsync(
                Enumerable.Range(0, backup.Chunks.Ids.Count),
                options,
                async (i, ct) =>
                {
                    int id = backup.Chunks.Ids[i];

                    string fileName = $"{id}.{_config.Chunk.Data.File}";
                    string path = System.IO.Path.Combine([GetPathChunks(), fileName]);
                    string content = await File.ReadAllTextAsync(path, ct);

                    Chunk? chunk = JsonConvert.DeserializeObject<Chunk>(content);

                    if (chunk is null)
                        throw new InvalidOperationException($"chunk null: {path}");

                    chunks[i] = chunk;

                    _logger.LogInformation("chunk {chunk} processed", i);
                }
            );
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
            throw;
        }
        catch
        {
            return null;
        }

        return [.. chunks];
    }

    public async Task<Stream?> GetChunk(Chunk chunk)
    {
        if (_config.Chunk.Zip.File is null)
            return null;

        string fileName = $"{chunk.Id}.{_config.Chunk.Zip.File}";
        string path = System.IO.Path.Combine(GetPathChunks(), fileName);

        string? directory = System.IO.Path.GetDirectoryName(path);

        if (directory is null)
            throw new Exception("error in GetDirectoryName.");

        Directory.CreateDirectory(directory);

        Stream fs = new FileStream(
            path,
            FileMode.OpenOrCreate,
            FileAccess.ReadWrite,
            FileShare.None,
            bufferSize: 128 * 1024,
            options: FileOptions.Asynchronous | FileOptions.SequentialScan
        );

        return fs;
    }

    public async Task<string?> GetHash(string path)
    {
        string fullPath = System.IO.Path.Combine(GetPathChunks(), path);
        string? hash = await Utils.FileHasher.GetFileHash(fullPath);

        return hash;
    }

    public async Task Save(List<Chunk> chunks)
    {
        foreach (Chunk chunk in chunks)
        {
            string fileName = $"{chunk.Id}.{_config.Chunk.Data.File}";
            string path = System.IO.Path.Combine(GetPathChunks(), fileName);

            string json = JsonConvert.SerializeObject(chunk, Formatting.Indented);
            await File.WriteAllTextAsync(path, json);
        }
    }

    public async Task SaveBackup(Models.Media.Backup.Backup backup)
    {
        if (_config.Chunk.File is null)
            throw new Exception("file not configured");

        string path = System.IO.Path.Combine(GetPathChunks(), _config.Chunk.File);
        string json = JsonConvert.SerializeObject(backup, Formatting.Indented);

        await File.WriteAllTextAsync(path, json);
    }

    public Task DeleteChunk(Chunk chunk)
    {
        string fileName = $"{chunk.Id}.{_config.Chunk.Zip.File}";
        string path = System.IO.Path.Combine(GetPathChunks(), fileName);

        if (!File.Exists(path))
            return Task.CompletedTask;

        File.Delete(path);

        return Task.CompletedTask;
    }

    public Task<bool> Exists(string path) =>
        Task.FromResult(File.Exists(System.IO.Path.Combine([GetPathDirect(), path])));

    public async Task<Stream> Write(string path)
    {
        string fullPath = System.IO.Path.Combine([GetPathDirect(), path]);
        string? directory = System.IO.Path.GetDirectoryName(fullPath);

        if (directory is null)
            throw new Exception("Error getting the directory name.");

        Directory.CreateDirectory(directory);
        Stream stream = File.Open(fullPath, FileMode.Create, FileAccess.Write, FileShare.None);

        return stream;
    }
}
