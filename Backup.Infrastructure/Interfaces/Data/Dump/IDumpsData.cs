using Backup.Infrastructure.Models.Dump;

namespace Backup.Infrastructure.Interfaces.Data.Posts;

public interface IDumpsData
{
    public Task<DumpsData> GetData();
    public Task Save(DumpsData dumps);
}

