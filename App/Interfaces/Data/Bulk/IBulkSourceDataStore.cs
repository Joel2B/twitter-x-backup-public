namespace Backup.App.Interfaces.Data.Bulk;

public interface IBulkSourceDataStore : IBulkSourceData
{
    public bool IsDefault { get; set; }
}
