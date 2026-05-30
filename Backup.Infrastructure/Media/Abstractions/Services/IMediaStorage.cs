using Backup.Infrastructure.Models.Media;

namespace Backup.Infrastructure.Media.Abstractions.Services;

public interface IMediaStorage
{
    public string? Id { get; set; }
    public Task Save(Stream stream, string path, CancellationToken token);
    public Task<bool> Exists(string path);
    public Task<Stream> Read(string path);
    public Task<Stream> Write(string path);
    public Task<string?> GetHash(string path);
    public Task<MediaCacheEntry?> GetCache(string path);
    public Stream GetTempStream();
}
