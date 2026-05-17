using Backup.App.Models.Bulk;

namespace Backup.App.Interfaces.Data.Bulk;

public interface IBulkData
{
    public string? Id { get; set; }
    public Task<List<BulkData>?> GetBulks();
    public Task Save(List<BulkData> bulks);
    public Task Prune();
}
