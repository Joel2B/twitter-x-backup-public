namespace Backup.App.Interfaces.Data.Bulk;

public interface IBulkDataStore : IBulkData
{
    public bool IsDefault { get; set; }
}
