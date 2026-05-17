using Backup.App.Models.Dump;

namespace Backup.App.Interfaces.Data.Posts;

public interface IDumpsData
{
    public Task<DumpsData> GetData();
    public Task Save(DumpsData dumps);
}
