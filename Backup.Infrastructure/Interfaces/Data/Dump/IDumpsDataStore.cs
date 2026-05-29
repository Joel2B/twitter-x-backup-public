namespace Backup.Infrastructure.Interfaces.Data.Posts;

public interface IDumpsDataStore : IDumpsData
{
    public bool IsDefault { get; set; }
}

