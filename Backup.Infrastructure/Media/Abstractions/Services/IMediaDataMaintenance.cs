using Backup.Infrastructure.Media.Models;

namespace Backup.Infrastructure.Media.Abstractions.Services;

public interface IMediaDataMaintenance
{
    public string? Id { get; set; }
    public Task<int> GetCacheCount(CancellationToken cancellationToken = default);
    public Task CheckData(List<Download> downloads, CancellationToken cancellationToken = default);
    public Task Prune(List<Download> downloads, CancellationToken cancellationToken = default);
    public Task CheckIntegrity(
        List<Download> downloads,
        CancellationToken cancellationToken = default
    );
}
