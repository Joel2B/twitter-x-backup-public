using Backup.App.Models.Media.Backup;

namespace Backup.App.Interfaces.Data.Media;

public interface IMediaBackup
{
    public Task<Models.Media.Backup.Backup?> GetBackup();
    public Task<List<Chunk>?> GetChunks(CancellationToken token = default);
    public Task<Stream?> GetChunk(Chunk chunk);
    public Task<string?> GetHash(string path);
    public Task Save(List<Chunk> chunks);
    public Task SaveBackup(Models.Media.Backup.Backup backup);
    public Task DeleteChunk(Chunk chunk);
    public Task<bool> Exists(string path);
    public Task<Stream> Write(string path);
}
