namespace Backup.App.Interfaces.Data.Posts;

public interface IDumpDataStore : IDumpData
{
    public bool IsDefault { get; set; }
}
