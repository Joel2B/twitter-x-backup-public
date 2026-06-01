using Backup.Infrastructure.Bulk.Models;

namespace Backup.Infrastructure.Bulk.Abstractions.Data;

public interface IBulkData
{
    public string? Id { get; set; }
    public Task<List<BulkData>?> GetBulks(CancellationToken cancellationToken = default);
    public Task Save(List<BulkData> bulks, CancellationToken cancellationToken = default);
    public Task Prune(CancellationToken cancellationToken = default);
}
