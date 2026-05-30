using Backup.Infrastructure.Models.Config;
using Backup.Infrastructure.Models.Config.Data;

namespace Backup.Infrastructure.Core.Abstractions.Config;

public interface IAppConfigStore
{
    public AppConfig Load();
    public DataConfig LoadData();
    public void SaveData(DataConfig data);
}
