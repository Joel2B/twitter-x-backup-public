using Backup.Infrastructure.Core.Abstractions.Setup;
using Backup.Infrastructure.Models.Media;

namespace Backup.Infrastructure.Media.Abstractions.Services;

public interface IMediaCache : ISetup
{
    public Task Load();
    public Task<string> GetPath(string path, long size = 0, CancellationToken ct = default);
    public MediaCacheEntry? Get(string path);
}
