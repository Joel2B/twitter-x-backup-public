using Backup.Infrastructure.Models.Dump;

namespace Backup.Infrastructure.Dump.Abstractions.Data;

public interface IDumpsData
{
    public Task<DumpsData> GetData();
    public Task Save(DumpsData dumps);
}
