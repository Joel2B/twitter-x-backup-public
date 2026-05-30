using Backup.Infrastructure.Models.Bulk;

namespace Backup.Infrastructure.Bulk.Abstractions.Data;

public interface IBulkSourceData
{
    public Task<List<Source>> GetSources();
}
