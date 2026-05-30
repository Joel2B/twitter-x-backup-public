using Backup.Infrastructure.Interfaces.Data;

namespace Backup.Infrastructure.Dump.Abstractions.Data;

public interface IDumpDataStore : IDumpData, IDefaultStore
{
}
