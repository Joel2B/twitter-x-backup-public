using Backup.Infrastructure.Core.Abstractions.Setup;
using Backup.Infrastructure.Media.Abstractions.Data;
using Backup.Infrastructure.Core.Abstractions.Partition;
using Backup.Infrastructure.Models.Config.Data;
using Backup.Infrastructure.Models.Config.Data.Backup;
using Backup.Infrastructure.Media.Models.Backup;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace Backup.Infrastructure.Media.Data;

public class LocalMediaBackup(
    ILogger<LocalMediaBackup> _logger,
    StorageBackup _config,
    IPartition _partition
) : IMediaBackupData, ISetup
{
    private readonly ILogger<LocalMediaBackup> _logger = _logger;
    private readonly StorageBackup _config = _config;
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
        PartitionConfig? backup = _partition
            .GetPartitions()
            .FirstOrDefault(o => o.Type == "backup");

        if (backup is null)
            throw new Exception();

        return Path.Combine([.. backup.Paths, .. _config.Paths.Paths]);
    }

    private string GetPathChunks() => Path.Combine([GetPath(), .. _config.Chunk.Paths]);

    private string GetPathDirect() => Path.Combine([GetPath(), .. _config.Direct.Paths]);

    public async Task<BackupChunks?> GetBackup()
    {
        if (_config.Chunk.File is null)
            return null;

        string path = Path.Combine(GetPathChunks(), _config.Chunk.File);

        if (!File.Exists(path))
            return null;

        string content = await File.ReadAllTextAsync(path);

        BackupChunks? backup = JsonConvert.DeserializeObject<BackupChunks>(content);

        if (backup is null)
            return null;

        return backup;
    }

    public async Task<List<Chunk>?> GetChunks(CancellationToken token = default)
    {
        if (_config.Chunk.Data.File is null)
            return null;

        BackupChunks? backup = await GetBackup();

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
                    string path = Path.Combine([GetPathChunks(), fileName]);
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

    public Task<Stream?> GetChunk(Chunk chunk)
    {
        if (_config.Chunk.Zip.File is null)
            return Task.FromResult<Stream?>(null);

        string fileName = $"{chunk.Id}.{_config.Chunk.Zip.File}";
        string path = Path.Combine(GetPathChunks(), fileName);
        string? directory = Path.GetDirectoryName(path);

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
        return Task.FromResult<Stream?>(fs);
    }

    public async Task<string?> GetHash(string path)
    {
        string fullPath = Path.Combine(GetPathChunks(), path);
        string? hash = await Backup.Infrastructure.Utils.FileHasher.GetFileHash(fullPath);

        return hash;
    }

    public async Task Save(List<Chunk> chunks)
    {
        foreach (Chunk chunk in chunks)
        {
            string fileName = $"{chunk.Id}.{_config.Chunk.Data.File}";
            string path = Path.Combine(GetPathChunks(), fileName);

            string json = JsonConvert.SerializeObject(chunk, Formatting.Indented);
            await File.WriteAllTextAsync(path, json);
        }
    }

    public async Task SaveBackup(BackupChunks backup)
    {
        if (_config.Chunk.File is null)
            throw new Exception("file not configured");

        string path = Path.Combine(GetPathChunks(), _config.Chunk.File);
        string json = JsonConvert.SerializeObject(backup, Formatting.Indented);

        await File.WriteAllTextAsync(path, json);
    }

    public Task DeleteChunk(Chunk chunk)
    {
        string fileName = $"{chunk.Id}.{_config.Chunk.Zip.File}";
        string path = Path.Combine(GetPathChunks(), fileName);

        if (!File.Exists(path))
            return Task.CompletedTask;

        File.Delete(path);

        return Task.CompletedTask;
    }

    public Task<bool> Exists(string path) =>
        Task.FromResult(File.Exists(Path.Combine([GetPathDirect(), path])));

    public async Task<Stream> Write(string path)
    {
        string fullPath = Path.Combine([GetPathDirect(), path]);
        string? directory = Path.GetDirectoryName(fullPath);

        if (directory is null)
            throw new Exception("Error getting the directory name.");

        Directory.CreateDirectory(directory);
        Stream stream = File.Open(fullPath, FileMode.Create, FileAccess.Write, FileShare.None);

        return stream;
    }
}
