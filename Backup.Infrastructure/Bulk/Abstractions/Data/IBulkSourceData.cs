using Backup.Infrastructure.Bulk.Models;

namespace Backup.Infrastructure.Bulk.Abstractions.Data;

public interface IBulkSourceData
{
    public Task<List<Source>> GetSources();
}
