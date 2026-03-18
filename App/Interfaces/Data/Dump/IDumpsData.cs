using Backup.App.Models.Dump;

namespace Backup.App.Interfaces.Data.Post;

public interface IDumpsData
{
    public Task<DumpsData> GetData();
    public Task Save(DumpsData dumps);
}
