using Backup.Infrastructure.Models.Dump;

namespace Backup.Infrastructure.Dump.Abstractions.Data;

public interface IDumpsData
{
    public Task<DumpsData> GetData(CancellationToken cancellationToken = default);
    public Task Save(DumpsData dumps, CancellationToken cancellationToken = default);
}
