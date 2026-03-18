using Backup.App.Models.Bulk;

namespace Backup.App.Interfaces.Data.Bulk;

public interface IBulkSourceData
{
    public Task<List<Source>> GetSources();
}
