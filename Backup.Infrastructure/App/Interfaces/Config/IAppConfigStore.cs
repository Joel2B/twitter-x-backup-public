using Backup.App.Models.Config;
using Backup.App.Models.Config.Data;

namespace Backup.App.Interfaces.Config;

public interface IAppConfigStore
{
    public AppConfig Load();
    public DataConfig LoadData();
    public void SaveData(DataConfig data);
}
