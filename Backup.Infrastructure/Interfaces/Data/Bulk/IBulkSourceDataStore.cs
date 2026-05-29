namespace Backup.Infrastructure.Interfaces.Data.Bulk;

public interface IBulkSourceDataStore : IBulkSourceData
{
    public bool IsDefault { get; set; }
}

