using Backup.Infrastructure.Models.Bulk;

namespace Backup.Infrastructure.Interfaces.Data.Bulk;

public interface IBulkSourceData
{
    public Task<List<Source>> GetSources();
}

