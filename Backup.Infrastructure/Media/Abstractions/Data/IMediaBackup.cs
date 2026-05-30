using Backup.Infrastructure.Media.Models.Backup;

namespace Backup.Infrastructure.Media.Abstractions.Data;

public interface IMediaBackupData
{
    public Task<BackupChunks?> GetBackup();
    public Task<List<Chunk>?> GetChunks(CancellationToken token = default);
    public Task<Stream?> GetChunk(Chunk chunk);
    public Task<string?> GetHash(string path);
    public Task Save(List<Chunk> chunks);
    public Task SaveBackup(BackupChunks backup);
    public Task DeleteChunk(Chunk chunk);
    public Task<bool> Exists(string path);
    public Task<Stream> Write(string path);
}
