namespace Backup.Infrastructure.Interfaces.Data.Posts;

public interface IDumpDataStore : IDumpData
{
    public bool IsDefault { get; set; }
}

