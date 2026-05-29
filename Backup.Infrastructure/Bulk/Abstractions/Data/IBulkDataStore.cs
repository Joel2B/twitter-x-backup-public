namespace Backup.Infrastructure.Interfaces.Data.Bulk;

public interface IBulkDataStore : IBulkData
{
    public bool IsDefault { get; set; }
}

