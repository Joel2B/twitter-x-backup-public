using Backup.Infrastructure.Models.Bulk;

namespace Backup.Infrastructure.Bulk.Abstractions.Data;

public interface IBulkData
{
    public string? Id { get; set; }
    public Task<List<BulkData>?> GetBulks();
    public Task Save(List<BulkData> bulks);
    public Task Prune();
}
