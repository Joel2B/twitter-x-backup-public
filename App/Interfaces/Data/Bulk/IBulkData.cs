namespace Backup.App.Interfaces.Data.Bulk;

public interface IBulkData
{
    public string? Id { get; set; }
    public Task<List<Models.Bulk.Bulk>?> GetBulks();
    public Task Save(List<Models.Bulk.Bulk> bulks);
    public Task Prune();
}
