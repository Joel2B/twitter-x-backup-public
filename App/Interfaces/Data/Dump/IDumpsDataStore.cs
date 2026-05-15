namespace Backup.App.Interfaces.Data.Post;

public interface IDumpsDataStore : IDumpsData
{
    public bool IsDefault { get; set; }
}
