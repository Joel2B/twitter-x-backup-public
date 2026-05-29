using Backup.App.Models.Media;

namespace Backup.App.Interfaces.Services.Media;

public interface IMediaData
{
    public string? Id { get; set; }
    public Task Save(Stream stream, string path, CancellationToken token);
    public Task CheckData(List<Download> downloads);
    public Task Prune(List<Download> downloads);
    public Task CheckIntegrity(List<Download> downloads);
    public Task<bool> Exists(string path);
    public Task<Stream> Read(string path);
    public Task<Stream> Write(string path);
    public Task<string?> GetHash(string path);
    public Task<Cache?> GetCache(string path);
    public Stream GetTempStream();
}
