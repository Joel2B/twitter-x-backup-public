namespace Backup.App.Interfaces.Data.Post;

public interface IDumpDataStore : IDumpData
{
    public bool IsDefault { get; set; }
}
